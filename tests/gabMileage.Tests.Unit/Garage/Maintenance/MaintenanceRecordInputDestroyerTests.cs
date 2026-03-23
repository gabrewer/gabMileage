using gabMileage.Api.Garage.Maintenance.Validation;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceRecordInputDestroyerTests
{
    private static readonly IClock Clock = new FakeClock(Instant.FromUtc(2026, 3, 23, 12, 0));

    [Fact]
    public void Validate_WhenDateIsFuture_ShouldReturnBriefFutureDateError()
    {
        var ex = Should.Throw<gabMileage.Api.Garage.Maintenance.MaintenanceValidationException>(() =>
            MaintenanceRecordInput.Validate(
                date: new LocalDate(2026, 3, 24),
                odometer: 123,
                notes: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("date");
        ex.Errors["date"].ShouldBe(["Date can't be in the future"]);
    }

    [Fact]
    public void Validate_WhenOdometerTooLarge_ShouldReturnBriefOdometerError()
    {
        var ex = Should.Throw<gabMileage.Api.Garage.Maintenance.MaintenanceValidationException>(() =>
            MaintenanceRecordInput.Validate(
                date: new LocalDate(2026, 3, 23),
                odometer: 10_000_000,
                notes: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("odometer");
        ex.Errors["odometer"].ShouldBe(["Enter a valid odometer reading"]);
    }

    [Fact]
    public void Validate_WhenNotesTooLong_ShouldReturnBriefNotesError()
    {
        var notes = new string('x', 501);

        var ex = Should.Throw<gabMileage.Api.Garage.Maintenance.MaintenanceValidationException>(() =>
            MaintenanceRecordInput.Validate(
                date: new LocalDate(2026, 3, 23),
                odometer: 100,
                notes: notes,
                clock: Clock));

        ex.Errors.ShouldContainKey("notes");
        ex.Errors["notes"].ShouldBe(["Notes are too long"]);
    }

    private sealed class FakeClock : IClock
    {
        private readonly Instant _instant;

        public FakeClock(Instant instant) => _instant = instant;

        public Instant GetCurrentInstant() => _instant;
    }
}
