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
    [InlineData("9999999999999999.99", "Heavy")]
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

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    public void Resolve_RejectsNonPositiveWeight(int weight)
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => WeightCategoryRules.Resolve(weight, DefaultCategories()));
    }

    [Theory]
    [InlineData("empty", "At least one")]
    [InlineData("missing-start", "start at 0")]
    [InlineData("negative-start", "negative")]
    [InlineData("zero-width", "exceed minimum")]
    [InlineData("reversed-range", "exceed minimum")]
    [InlineData("finite-end", "no upper limit")]
    [InlineData("unbounded-middle", "only the final")]
    public void Validate_RejectsIncompleteOrInvalidRanges(string scenario, string expectedError)
    {
        var categories = DefaultCategories();
        switch (scenario)
        {
            case "empty": categories.Clear(); break;
            case "missing-start": categories[0].MinWeightKg = 1m; break;
            case "negative-start": categories[0].MinWeightKg = -1m; break;
            case "zero-width": categories[1].MaxWeightKg = 500m; break;
            case "reversed-range": categories[1].MaxWeightKg = 400m; break;
            case "finite-end": categories[2].MaxWeightKg = 10000m; break;
            case "unbounded-middle": categories[1].MaxWeightKg = null; break;
        }

        Assert.Contains(WeightCategoryRules.Validate(categories), error => error.Contains(expectedError));
    }

    [Fact]
    public void ValidateAndResolve_AcceptUnorderedCategories()
    {
        var categories = DefaultCategories();
        categories.Reverse();

        Assert.Empty(WeightCategoryRules.Validate(categories));
        Assert.Equal("Medium", WeightCategoryRules.Resolve(500m, categories).Name);
    }

    [Fact]
    public void SingleUnboundedCategory_CoversAllValidWeights()
    {
        var category = new VehicleCategory
        {
            Name = "All vehicles", IconName = "light.svg", MinWeightKg = 0m
        };

        Assert.Empty(WeightCategoryRules.Validate([category]));
        Assert.Same(category, WeightCategoryRules.Resolve(0.01m, [category]));
        Assert.Same(category, WeightCategoryRules.Resolve(9999999999999999.99m, [category]));
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
