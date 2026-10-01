using CreditWorks.Web.Data;
using CreditWorks.Web.Models;
using CreditWorks.Web.Pages.Vehicles;
using CreditWorks.Web.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace CreditWorks.Tests.Integration;

public class CategoryPersistenceTests(SqlServerFixture database) : SqlServerTest(database)
{
    [SqlFact]
    public async Task MigrationsAndSeeder_CreateRequiredDataAndCanBeRepeated()
    {
        await using var db = Database.CreateContext();
        await db.Database.MigrateAsync();
        DatabaseSeeder.Seed(db);

        Assert.Empty(await db.Database.GetPendingMigrationsAsync());
        Assert.Equal(new[] { "Ferrari", "Honda", "Mazda", "Mercedes", "Toyota" },
            await db.Manufacturers.OrderBy(m => m.Name).Select(m => m.Name).ToArrayAsync());
        var categories = await db.VehicleCategories.OrderBy(c => c.MinWeightKg).ToListAsync();
        Assert.Equal(new[] { "Light", "Medium", "Heavy" }, categories.Select(c => c.Name));
        Assert.Equal(new[] { "light.svg", "medium.svg", "heavy.svg" }, categories.Select(c => c.IconName));
        Assert.Equal(new[] { 0m, 500m, 2500m }, categories.Select(c => c.MinWeightKg));
        Assert.Equal(new decimal?[] { 500m, 2500m, null }, categories.Select(c => c.MaxWeightKg));
    }

    [SqlFact]
    public async Task Save_CreatesEditsAndDeletesCategoriesTogether()
    {
        await using var db = Database.CreateContext();
        var drafts = await LoadDrafts(db);
        var removedId = drafts[1].Id;
        drafts.RemoveAt(1);
        drafts[0].Name = "  Small  ";
        drafts[0].IconName = "medium.svg";
        drafts[0].MaxWeightKg = 1000m;
        drafts.Add(new CategoryDraft
        {
            Name = "New medium", IconName = "light.svg", MinWeightKg = 1000m, MaxWeightKg = 2500m
        });

        Assert.Empty(await new CategoryConfigurationService(db).SaveAsync(drafts));

        await using var verification = Database.CreateContext();
        var saved = await LoadDrafts(verification);
        Assert.Equal(new[] { "Small", "New medium", "Heavy" }, saved.Select(c => c.Name));
        Assert.Equal("medium.svg", saved[0].IconName);
        Assert.Equal(1000m, saved[0].MaxWeightKg);
        Assert.Equal(1000m, saved[1].MinWeightKg);
        Assert.Equal(2500m, saved[1].MaxWeightKg);
        Assert.DoesNotContain(saved, c => c.Id == removedId);
        Assert.True(saved[1].Id > 0);
    }

    [SqlFact]
    public async Task Save_NewBoundariesReclassifyExistingVehicleOnNextListLoad()
    {
        await using var db = Database.CreateContext();
        var vehicle = new Vehicle
        {
            OwnerName = "Existing owner", ManufacturerId = await db.Manufacturers.Select(m => m.Id).FirstAsync(),
            YearOfManufacture = 2020, WeightKg = 2200m
        };
        db.Vehicles.Add(vehicle);
        await db.SaveChangesAsync();
        var before = new IndexModel(db);
        await before.OnGetAsync();
        Assert.Equal("Medium", Assert.Single(before.Vehicles).CategoryName);

        var drafts = await LoadDrafts(db);
        drafts[1].MaxWeightKg = 2000m;
        drafts[2].MinWeightKg = 2000m;
        Assert.Empty(await new CategoryConfigurationService(db).SaveAsync(drafts));

        await using var verification = Database.CreateContext();
        var after = new IndexModel(verification);
        await after.OnGetAsync();
        var row = Assert.Single(after.Vehicles);
        Assert.Equal("Heavy", row.CategoryName);
        Assert.Equal("heavy.svg", row.CategoryIconName);
        var persisted = Assert.Single(await verification.Vehicles.ToListAsync());
        Assert.Equal(vehicle.Id, persisted.Id);
        Assert.Equal(vehicle.OwnerName, persisted.OwnerName);
        Assert.Equal(vehicle.ManufacturerId, persisted.ManufacturerId);
        Assert.Equal(vehicle.YearOfManufacture, persisted.YearOfManufacture);
        Assert.Equal(2200m, persisted.WeightKg);
    }

    [SqlTheory]
    [InlineData("gap", "gap")]
    [InlineData("overlap", "overlap")]
    [InlineData("delete-middle", "gap")]
    [InlineData("delete-all", "At least one")]
    [InlineData("stale-id", "Reload")]
    public async Task Save_RejectedConfigurationLeavesStoredCategoriesUnchanged(string scenario, string expectedError)
    {
        await using var db = Database.CreateContext();
        var before = await Snapshot(db);
        var drafts = await LoadDrafts(db);
        drafts[0].Name = "Should not be saved";
        switch (scenario)
        {
            case "gap": drafts[1].MinWeightKg = 600m; break;
            case "overlap": drafts[1].MinWeightKg = 400m; break;
            case "delete-middle": drafts.RemoveAt(1); break;
            case "delete-all": drafts.Clear(); break;
            case "stale-id": drafts[1].Id = int.MaxValue; break;
        }

        var errors = await new CategoryConfigurationService(db).SaveAsync(drafts);

        Assert.Contains(errors, error => error.Contains(expectedError));
        await using var verification = Database.CreateContext();
        Assert.Equal(before, await Snapshot(verification));
    }

    [SqlFact]
    public async Task Save_FailureAfterDatabaseWritesRollsBackEntireConfiguration()
    {
        await using var verification = Database.CreateContext();
        var before = await Snapshot(verification);
        var failure = new FailAfterSaveInterceptor();
        await using (var db = Database.CreateContext(failure))
        {
            var drafts = await LoadDrafts(db);
            drafts[0].Name = "Should roll back";
            drafts[0].MaxWeightKg = 1000m;
            drafts.RemoveAt(1);
            drafts.Add(new CategoryDraft
            {
                Name = "Should disappear", IconName = "medium.svg", MinWeightKg = 1000m, MaxWeightKg = 2500m
            });

            await Assert.ThrowsAsync<SimulatedSaveFailure>(
                () => new CategoryConfigurationService(db).SaveAsync(drafts));
            Assert.True(failure.WritesCompleted);
        }

        // Read through a fresh context: tracked entities cannot prove that a transaction rolled back.
        await using var after = Database.CreateContext();
        Assert.Equal(before, await Snapshot(after));
    }

    private static Task<List<CategoryDraft>> LoadDrafts(AppDbContext db) =>
        db.VehicleCategories.AsNoTracking().OrderBy(c => c.MinWeightKg)
            .Select(c => new CategoryDraft
            {
                Id = c.Id, Name = c.Name, IconName = c.IconName,
                MinWeightKg = c.MinWeightKg, MaxWeightKg = c.MaxWeightKg
            }).ToListAsync();

    private static async Task<CategoryState[]> Snapshot(AppDbContext db) =>
        (await LoadDrafts(db)).Select(c => new CategoryState(
            c.Id, c.Name, c.IconName, c.MinWeightKg, c.MaxWeightKg)).ToArray();

    private sealed record CategoryState(int Id, string Name, string Icon, decimal? Minimum, decimal? Maximum);
    private sealed class SimulatedSaveFailure : Exception;

    private sealed class FailAfterSaveInterceptor : SaveChangesInterceptor
    {
        public bool WritesCompleted { get; private set; }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result,
            CancellationToken cancellationToken = default)
        {
            WritesCompleted = true;
            throw new SimulatedSaveFailure();
        }
    }
}
