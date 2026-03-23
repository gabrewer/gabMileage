namespace gabMileage.Api.Garage.Maintenance;

public sealed class MaintenanceItemNotFoundException : Exception
{
    public MaintenanceItemNotFoundException(Guid itemId)
        : base($"Maintenance item {itemId:D} not found.")
    {
        ItemId = itemId;
    }

    public Guid ItemId { get; }
}
