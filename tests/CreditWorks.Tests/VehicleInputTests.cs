using System.ComponentModel.DataAnnotations;
using CreditWorks.Web.Pages.Vehicles;

namespace CreditWorks.Tests;

public class VehicleInputTests
{
    [Theory]
    [InlineData("OwnerName")]
    [InlineData("WhitespaceOwner")]
    [InlineData("LongOwner")]
    [InlineData("ManufacturerId")]
    [InlineData("YearOfManufacture")]
    [InlineData("WeightKg")]
    public void Input_RejectsMissingFieldsAndInvalidOwner(string scenario)
    {
        var input = new CreateModel.VehicleInput
        {
            OwnerName = "Jane Turei", ManufacturerId = 1,
            YearOfManufacture = 2020, WeightKg = 1850.75m
        };

        var expectedField = scenario;
        switch (scenario)
        {
            case "OwnerName": input.OwnerName = ""; break;
            case "WhitespaceOwner": input.OwnerName = "   "; expectedField = "OwnerName"; break;
            case "LongOwner": input.OwnerName = new string('x', 151); expectedField = "OwnerName"; break;
            case "ManufacturerId": input.ManufacturerId = null; break;
            case "YearOfManufacture": input.YearOfManufacture = null; break;
            case "WeightKg": input.WeightKg = null; break;
        }

        // Data annotations are normally invoked by MVC before a page handler runs.
        var errors = new List<ValidationResult>();
        Assert.False(Validator.TryValidateObject(input, new ValidationContext(input), errors, true));
        Assert.Contains(errors, error => error.MemberNames.Contains(expectedField));
    }

    [Fact]
    public void Input_AcceptsOwnerAtMaximumLength()
    {
        var input = new CreateModel.VehicleInput
        {
            OwnerName = new string('x', 150), ManufacturerId = 1,
            YearOfManufacture = 2020, WeightKg = 1850.75m
        };

        Assert.True(Validator.TryValidateObject(input, new ValidationContext(input), [], true));
    }
}
