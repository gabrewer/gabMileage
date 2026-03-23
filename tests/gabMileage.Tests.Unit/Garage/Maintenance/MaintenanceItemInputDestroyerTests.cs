using gabMileage.Api.Garage.Maintenance;
using gabMileage.Api.Garage.Maintenance.Validation;
using NodaTime;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.Maintenance;

public sealed class MaintenanceItemInputDestroyerTests
{
    private static readonly IClock Clock = new FakeClock(Instant.FromUtc(2026, 3, 23, 12, 0));

    [Fact]
    public void NormalizeAndValidate_WhenNameIsWhitespace_ShouldReturnBriefNameError()
    {
        var ex = Should.Throw<MaintenanceValidationException>(() =>
            MaintenanceItemInput.NormalizeAndValidate(
                name: "   ",
                mileageInterval: 5000,
                timeIntervalMonths: null,
                mileageWarning: 500,
                timeWarningDays: 14,
                initialLastPerformedDate: null,
                initialLastPerformedOdometer: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("name");
        ex.Errors["name"].ShouldBe(["Give this item a name"]);
    }

    [Fact]
    public void NormalizeAndValidate_WhenNameTooLong_ShouldStillUseBriefNameError()
    {
        var name = new string('a', 101);

        var ex = Should.Throw<MaintenanceValidationException>(() =>
            MaintenanceItemInput.NormalizeAndValidate(
                name: name,
                mileageInterval: 5000,
                timeIntervalMonths: null,
                mileageWarning: 500,
                timeWarningDays: 14,
                initialLastPerformedDate: null,
                initialLastPerformedOdometer: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("name");
        ex.Errors["name"].ShouldBe(["Give this item a name"]);
    }

    [Fact]
    public void NormalizeAndValidate_WhenNoIntervalsProvided_ShouldReturnIntervalsRequiredError()
    {
        var ex = Should.Throw<MaintenanceValidationException>(() =>
            MaintenanceItemInput.NormalizeAndValidate(
                name: "Oil change",
                mileageInterval: null,
                timeIntervalMonths: null,
                mileageWarning: 500,
                timeWarningDays: 14,
                initialLastPerformedDate: null,
                initialLastPerformedOdometer: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("intervals");
        ex.Errors["intervals"].ShouldBe(["Set a mileage interval, time interval, or both"]);
    }

    [Fact]
    public void NormalizeAndValidate_WhenMileageWarningDefaultWouldExceedInterval_ShouldFailWarningLessThanInterval()
    {
        // System.Text.Json missing int -> 0; app treats 0 as default(500).
        // If user sets a small interval, default warning must not silently violate the invariant.
        var ex = Should.Throw<MaintenanceValidationException>(() =>
            MaintenanceItemInput.NormalizeAndValidate(
                name: "Rotate tires",
                mileageInterval: 300,
                timeIntervalMonths: null,
                mileageWarning: 0,
                timeWarningDays: 0,
                initialLastPerformedDate: null,
                initialLastPerformedOdometer: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("mileageWarning");
        ex.Errors["mileageWarning"].ShouldBe(["Warning threshold must be less than the interval"]);
    }

    [Fact]
    public void NormalizeAndValidate_WhenTimeWarningDaysAtOrBeyondInterval_ShouldFailWarningLessThanInterval()
    {
        // 1 month ~ 30.44 days; 31 should always be invalid.
        var ex = Should.Throw<MaintenanceValidationException>(() =>
            MaintenanceItemInput.NormalizeAndValidate(
                name: "Brake fluid",
                mileageInterval: null,
                timeIntervalMonths: 1,
                mileageWarning: 0,
                timeWarningDays: 31,
                initialLastPerformedDate: null,
                initialLastPerformedOdometer: null,
                clock: Clock));

        ex.Errors.ShouldContainKey("timeWarningDays");
        ex.Errors["timeWarningDays"].ShouldBe(["Warning threshold must be less than the interval"]);
    }

    [Fact]
    public void NormalizeAndValidate_WhenInitialLastPerformedDateIsFuture_ShouldFailWithCorrectFieldKey()
    {
        var future = new LocalDate(2026, 3, 24);

        var ex = Should.Throw<MaintenanceValidationException>(() =>
            MaintenanceItemInput.NormalizeAndValidate(
                name: "Oil change",
                mileageInterval: 5000,
                timeIntervalMonths: null,
                mileageWarning: 500,
                timeWarningDays: 14,
                initialLastPerformedDate: future,
                initialLastPerformedOdometer: 12345,
                clock: Clock));

        ex.Errors.ShouldContainKey("initialLastPerformedDate");
        ex.Errors["initialLastPerformedDate"].ShouldBe(["Date can't be in the future"]);
    }

    private sealed class FakeClock : IClock
    {
        private readonly Instant _instant;

        public FakeClock(Instant instant) => _instant = instant;

        public Instant GetCurrentInstant() => _instant;
    }
}
