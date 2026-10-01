using CreditWorks.Web.Data;
using CreditWorks.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Tests;

public class CategoryValidationTests
{
    [Theory]
    [InlineData("empty", "At least one")]
    [InlineData("negative-id", "IDs")]
    [InlineData("duplicate-id", "appear twice")]
    [InlineData("blank-name", "name")]
    [InlineData("long-name", "name")]
    [InlineData("duplicate-name", "unique")]
    [InlineData("missing-icon", "icons")]
    [InlineData("unknown-icon", "icons")]
    [InlineData("missing-minimum", "minimum weight")]
    [InlineData("negative-minimum", "non-negative")]
    [InlineData("minimum-precision", "decimal places")]
    [InlineData("maximum-precision", "decimal places")]
    [InlineData("minimum-overflow", "Minimum weights")]
    [InlineData("maximum-overflow", "Maximum weights")]
    [InlineData("gap", "gap")]
    [InlineData("overlap", "overlap")]
    public async Task Save_RejectsInvalidConfigurationBeforeAccessingDatabase(
        string scenario, string expectedError)
    {
        var drafts = new List<CategoryDraft>
        {
            new() { Id = 1, Name = "Light", IconName = "light.svg", MinWeightKg = 0m, MaxWeightKg = 500m },
            new() { Id = 2, Name = "Heavy", IconName = "heavy.svg", MinWeightKg = 500m }
        };

        switch (scenario)
        {
            case "empty": drafts.Clear(); break;
            case "negative-id": drafts[0].Id = -1; break;
            case "duplicate-id": drafts[1].Id = 1; break;
            case "blank-name": drafts[0].Name = "  "; break;
            case "long-name": drafts[0].Name = new string('x', 101); break;
            case "duplicate-name": drafts[1].Name = " LIGHT "; break;
            case "missing-icon": drafts[0].IconName = ""; break;
            case "unknown-icon": drafts[0].IconName = "../other.svg"; break;
            case "missing-minimum": drafts[0].MinWeightKg = null; break;
            case "negative-minimum": drafts[0].MinWeightKg = -1m; break;
            case "minimum-precision": drafts[1].MinWeightKg = 500.001m; break;
            case "maximum-precision": drafts[0].MaxWeightKg = 500.001m; break;
            case "minimum-overflow": drafts[1].MinWeightKg = 10000000000000000m; break;
            case "maximum-overflow": drafts[0].MaxWeightKg = 10000000000000000m; break;
            case "gap": drafts[1].MinWeightKg = 600m; break;
            case "overlap": drafts[1].MinWeightKg = 400m; break;
        }

        // No provider: validation must return errors without attempting any database I/O.
        await using var db = new AppDbContext(new DbContextOptions<AppDbContext>());
        var errors = await new CategoryConfigurationService(db).SaveAsync(drafts);

        Assert.Contains(errors, error => error.Contains(expectedError, StringComparison.OrdinalIgnoreCase));
    }
}
