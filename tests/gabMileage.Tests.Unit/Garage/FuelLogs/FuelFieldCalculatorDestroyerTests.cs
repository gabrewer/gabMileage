using gabMileage.Api.Garage.FuelLogs;
using gabMileage.Api.Garage.FuelLogs.Validation;
using Shouldly;

namespace gabMileage.Tests.Unit.Garage.FuelLogs;

public sealed class FuelFieldCalculatorDestroyerTests
{
    [Fact]
    public void NormalizeAndValidate_WithFewerThanTwoFields_ShouldThrowWithExactMessage()
    {
        var ex = Should.Throw<FuelLogValidationException>(() =>
            FuelFieldCalculator.NormalizeAndValidate(
                gallons: 10m,
                totalCost: null,
                pricePerGallon: null,
                calculatedField: null));

        ex.Errors.ShouldContainKey("fuel");
        ex.Errors["fuel"].ShouldBe(["Enter at least two fuel fields — we'll calculate the third."]);
    }

    [Theory]
    [InlineData(0.0001)]
    [InlineData(1.23456)]
    public void NormalizeAndValidate_GallonsWithTooManyDecimals_ShouldReject(decimal gallons)
    {
        var ex = Should.Throw<FuelLogValidationException>(() =>
            FuelFieldCalculator.NormalizeAndValidate(
                gallons: gallons,
                totalCost: 1.00m,
                pricePerGallon: null,
                calculatedField: null));

        ex.Errors.ShouldContainKey("gallons");
    }

    [Fact]
    public void NormalizeAndValidate_AllowsZeroTotalCost_WhenOtherTwoFieldsProvidedOrDerivable()
    {
        // totalCost=0 is explicitly allowed.
        var (g, cost, ppg, calc) = FuelFieldCalculator.NormalizeAndValidate(
            gallons: 10.000m,
            totalCost: 0.00m,
            pricePerGallon: null,
            calculatedField: null);

        g.ShouldBe(10.000m);
        cost.ShouldBe(0.00m);
        ppg.ShouldBe(0.000m);
        calc.ShouldBe("pricePerGallon");
    }

    [Fact]
    public void NormalizeAndValidate_AllowsZeroPricePerGallon_WhenComputingTotalCost()
    {
        // pricePerGallon=0 is explicitly allowed.
        var (g, cost, ppg, calc) = FuelFieldCalculator.NormalizeAndValidate(
            gallons: 10.000m,
            totalCost: null,
            pricePerGallon: 0.000m,
            calculatedField: null);

        g.ShouldBe(10.000m);
        cost.ShouldBe(0.00m);
        ppg.ShouldBe(0.000m);
        calc.ShouldBe("totalCost");
    }

    [Fact]
    public void NormalizeAndValidate_InvalidCalculatedFieldString_ShouldReject()
    {
        var ex = Should.Throw<FuelLogValidationException>(() =>
            FuelFieldCalculator.NormalizeAndValidate(
                gallons: 10m,
                totalCost: 1m,
                pricePerGallon: 0.1m,
                calculatedField: "not-a-real-field"));

        ex.Errors.ShouldContainKey("calculatedField");
    }

    [Fact]
    public void NormalizeAndValidate_WhenAllThreeProvidedAndInconsistent_ShouldRecalculatePricePerGallonAndMarkCalculatedField()
    {
        // Adversarial: send a bogus pricePerGallon even though gallons+totalCost imply a different value.
        var (g, cost, ppg, calc) = FuelFieldCalculator.NormalizeAndValidate(
            gallons: 10.000m,
            totalCost: 30.00m,
            pricePerGallon: 1.000m,
            calculatedField: null);

        g.ShouldBe(10.000m);
        cost.ShouldBe(30.00m);
        ppg.ShouldBe(3.000m);
        calc.ShouldBe("pricePerGallon");
    }
}
