using gabMileage.Api.Users.DomainEvents;

namespace gabMileage.Api.Users;

public sealed class User
{
    public string Id { get; private set; } = "";
    public bool IsProvisioned { get; private set; }

    public void Apply(UserProvisioned e)
    {
        Id = e.UserId;
        IsProvisioned = true;
    }
}
