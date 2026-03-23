using Shouldly;

namespace gabMileage.Tests.Unit.Garage;

public sealed class AddVehicleHandlerDestroyerTests
{
    [Fact]
    public void AddVehicle_IdempotencyMarker_MustBeInsertOnly_ToPreventDuplicateVehicleCreationUnderRace()
    {
        // Threat model:
        // - Client retries or the user double-submits with the SAME commandId (idempotency key).
        // - Two concurrent requests both observe "no marker", then both start distinct vehicle streams.
        // - If the marker write is an UPSERT (Store), both requests can succeed and create TWO vehicles.
        //
        // Guardrail:
        // - The idempotency marker must be written with INSERT-only semantics so one request fails fast
        //   (unique violation) and the handler can treat it as idempotent success by re-loading.
        //
        // NOTE: This is a static guardrail test: it forces the handler to use session.Insert for the marker.
        // If this fails, the idempotency design is likely unsafe under concurrency.

        var root = RepoRoot.Find();
        var path = Path.Combine(root, "gabMileage.Api", "Garage", "Vehicles", "Handlers", "AddVehicleHandler.cs");
        File.Exists(path).ShouldBeTrue($"Expected '{path}' to exist.");

        var text = File.ReadAllText(path);

        text.ShouldContain("new ProcessedAddVehicle", Case.Sensitive);

        // Require Insert-only semantics (Store is typically an upsert in Marten and may not throw on conflict).
        text.ShouldContain("session.Insert", Case.Sensitive,
            "AddVehicleHandler should write ProcessedAddVehicle with session.Insert(...) to guarantee one-winner idempotency under concurrent requests with the same commandId.");
    }

    private static class RepoRoot
    {
        public static string Find()
        {
            var current = new DirectoryInfo(AppContext.BaseDirectory);

            while (current is not null)
            {
                if (File.Exists(Path.Combine(current.FullName, ".git")))
                    return current.FullName;

                current = current.Parent;
            }

            throw new InvalidOperationException("Could not locate repo root by walking up from AppContext.BaseDirectory.");
        }
    }
}
