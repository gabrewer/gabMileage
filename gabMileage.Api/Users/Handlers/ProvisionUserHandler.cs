using gabMileage.Api.Users.Commands;
using gabMileage.Api.Users.DomainEvents;
using Marten;
using NodaTime;
using Npgsql;

namespace gabMileage.Api.Users.Handlers;

public static class ProvisionUserHandler
{
    // Breadcrumbs (backend-builder)
    // What: Idempotent first-login user provisioning handler that emits UserProvisioned once per user stream.
    // Why: Gateway calls provisioning after successful auth; Marten projections build UserProfile read model immediately (Inline) for read-after-write.
    // Alternatives considered: Upsert document-only UserProfile (rejected: we want an event trail); Append duplicate UserProvisioned events (rejected: would break idempotency).
    // Confidence: high
    public static async Task<Events> Handle(
        ProvisionUser command,
        IDocumentSession session,
        IClock clock,
        CancellationToken cancellationToken)
    {
        var existing = await session.Events.AggregateStreamAsync<User>(command.UserId, token: cancellationToken);
        if (existing is { IsProvisioned: true })
            return Events.Empty;

        var provisioned = new UserProvisioned(
            command.UserId,
            command.DisplayName,
            command.Email,
            command.AvatarUrl,
            clock.GetCurrentInstant());

        session.Events.StartStream<User>(command.UserId, provisioned);

        try
        {
            await session.SaveChangesAsync(cancellationToken);
            return new Events(provisioned);
        }
        catch (Exception ex) when (IsUniqueViolation(ex))
        {
            // Breadcrumbs (backend-builder)
            // What: Treat concurrent StartStream+SaveChanges unique-violation as idempotent success.
            // Why: Two first-login requests can race; one will win stream creation, the other must not 500.
            // Alternatives considered: Remove pre-check and always append with expected version (more complex); distributed lock (rejected).
            // Confidence: high
            return Events.Empty;
        }
    }

    private static bool IsUniqueViolation(Exception ex)
    {
        for (var current = ex; current is not null; current = current.InnerException)
        {
            if (current is PostgresException pg && pg.SqlState == PostgresErrorCodes.UniqueViolation)
                return true;
        }

        return false;
    }
}
