using System.Globalization;
using CreditWorks.Web.Models;
using CreditWorks.Web.Services;
using Xunit;

namespace CreditWorks.Tests;

public class WeightCategoryRulesTests
{
    [Theory]
    [InlineData("0.01", "Light")]
    [InlineData("499.99", "Light")]
    [InlineData("500.00", "Medium")]
    [InlineData("2499.99", "Medium")]
    [InlineData("2500.00", "Heavy")]
    public void Resolve_UsesExactBoundaries(string weightText, string expectedName)
    {
        var weight = decimal.Parse(weightText, CultureInfo.InvariantCulture);

        var category = WeightCategoryRules.Resolve(weight, DefaultCategories());

        Assert.Equal(expectedName, category.Name);
    }

    [Fact]
    public void Validate_AcceptsCompleteCoverage()
    {
        Assert.Empty(WeightCategoryRules.Validate(DefaultCategories()));
    }

    [Fact]
    public void Validate_RejectsGap()
    {
        var categories = DefaultCategories();
        categories[1].MinWeightKg = 600m;

        var errors = WeightCategoryRules.Validate(categories);

        Assert.Contains(errors, error => error.Contains("gap"));
        Assert.Throws<InvalidOperationException>(
            () => WeightCategoryRules.Resolve(550m, categories));
    }

    [Fact]
    public void Validate_RejectsOverlap()
    {
        var categories = DefaultCategories();
        categories[1].MinWeightKg = 450m;

        var errors = WeightCategoryRules.Validate(categories);

        Assert.Contains(errors, error => error.Contains("overlap"));
        Assert.Throws<InvalidOperationException>(
            () => WeightCategoryRules.Resolve(475m, categories));
    }

    [Fact]
    public void Resolve_ReflectsChangedCategoryDefinitions()
    {
        var categories = DefaultCategories();
        Assert.Equal("Medium", WeightCategoryRules.Resolve(2200m, categories).Name);

        categories[1].MaxWeightKg = 2000m;
        categories[2].MinWeightKg = 2000m;

        Assert.Empty(WeightCategoryRules.Validate(categories));
        Assert.Equal("Heavy", WeightCategoryRules.Resolve(2200m, categories).Name);
    }

    private static List<VehicleCategory> DefaultCategories() =>
    [
        new() { Name = "Light", IconName = "light.svg",
                MinWeightKg = 0m, MaxWeightKg = 500m },
        new() { Name = "Medium", IconName = "medium.svg",
                MinWeightKg = 500m, MaxWeightKg = 2500m },
        new() { Name = "Heavy", IconName = "heavy.svg",
                MinWeightKg = 2500m, MaxWeightKg = null }
    ];
}
