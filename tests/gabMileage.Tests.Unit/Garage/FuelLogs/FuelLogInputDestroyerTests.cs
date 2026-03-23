using gabMileage.Api.Garage.FuelLogs;
using gabMileage.Api.Garage.FuelLogs.Validation;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelLogInputDestroyerTests
{
    [Fact]
    public void Validate_FutureDate_ShouldThrowWithExactMessage()
    {
        var clock = new FixedClock(Instant.FromUtc(2026, 3, 23, 12, 0));

        var ex = Should.Throw<FuelLogValidationException>(() =>
            FuelLogInput.Validate(
                date: new LocalDate(2026, 3, 24),
                odometer: 1,
                notes: null,
                clock: clock));

        ex.Errors.ShouldContainKey("date");
        ex.Errors["date"].ShouldBe(["Date can't be in the future"]);
    }

    [Fact]
    public void Validate_NotesLength_IsMeasuredAfterTrimming()
    {
        var clock = new FixedClock(Instant.FromUtc(2026, 3, 23, 12, 0));

        // 600 chars, but with leading/trailing whitespace that trims down to 500.
        var notes = " " + new string('a', 500) + new string(' ', 200);

        Should.NotThrow(() => FuelLogInput.Validate(
            date: new LocalDate(2026, 3, 23),
            odometer: 123,
            notes: notes,
            clock: clock));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(10_000_000)]
    public void Validate_InvalidOdometer_ShouldThrow(int odometer)
    {
        var clock = new FixedClock(Instant.FromUtc(2026, 3, 23, 12, 0));

        var ex = Should.Throw<FuelLogValidationException>(() => FuelLogInput.Validate(
            date: new LocalDate(2026, 3, 23),
            odometer: odometer,
            notes: null,
            clock: clock));

        ex.Errors.ShouldContainKey("odometer");
    }

    private sealed class FixedClock : IClock
    {
        private readonly Instant _instant;

        public FixedClock(Instant instant) => _instant = instant;

        public Instant GetCurrentInstant() => _instant;
    }
}
