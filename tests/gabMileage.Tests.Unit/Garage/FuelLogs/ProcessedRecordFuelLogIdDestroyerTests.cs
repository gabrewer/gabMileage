using gabMileage.Api.Garage.FuelLogs;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class ProcessedRecordFuelLogIdDestroyerTests
{
    [Fact]
    public void From_IsScopedOnlyToUserAndCommand_NotVehicle()
    {
        // Adversarial: if a client accidentally reuses a commandId across two vehicles,
        // the current id format will treat the second request as a duplicate and return
        // the first FuelLogId. This test documents the risk.
        var userId = "user_123";
        var commandId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");

        ProcessedRecordFuelLogId.From(userId, commandId)
            .ShouldBe("user_123::aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    }
}
