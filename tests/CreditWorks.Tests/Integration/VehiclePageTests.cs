using System.Globalization;
using CreditWorks.Web.Models;
using CreditWorks.Web.Pages.Vehicles;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace CreditWorks.Tests.Integration;

public class VehiclePageTests(SqlServerFixture database) : SqlServerTest(database)
{
    [SqlTheory]
    [InlineData("zero-weight", "Input.WeightKg")]
    [InlineData("negative-weight", "Input.WeightKg")]
    [InlineData("weight-precision", "Input.WeightKg")]
    [InlineData("weight-overflow", "Input.WeightKg")]
    [InlineData("old-year", "Input.YearOfManufacture")]
    [InlineData("future-year", "Input.YearOfManufacture")]
    [InlineData("unknown-manufacturer", "Input.ManufacturerId")]
    public async Task Create_RejectsInvalidBusinessValuesWithoutSaving(string scenario, string field)
    {
        await using var db = Database.CreateContext();
        var page = new CreateModel(db, NullLogger<CreateModel>.Instance)
        {
            Input = new()
            {
                OwnerName = "Jane Turei",
                ManufacturerId = await db.Manufacturers.Select(m => m.Id).FirstAsync(),
                YearOfManufacture = 2020, WeightKg = 1850.75m
            }
        };
        switch (scenario)
        {
            case "zero-weight": page.Input.WeightKg = 0m; break;
            case "negative-weight": page.Input.WeightKg = -1m; break;
            case "weight-precision": page.Input.WeightKg = 1850.751m; break;
            case "weight-overflow": page.Input.WeightKg = 10000000000000000m; break;
            case "old-year": page.Input.YearOfManufacture = 1885; break;
            case "future-year": page.Input.YearOfManufacture = DateTime.UtcNow.Year + 1; break;
            case "unknown-manufacturer": page.Input.ManufacturerId = int.MaxValue; break;
        }

        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.NotEmpty(page.ModelState[field]!.Errors);
        Assert.Equal(5, page.Manufacturers.Count);
        await using var verification = Database.CreateContext();
        Assert.Empty(await verification.Vehicles.ToListAsync());
    }

    [SqlTheory]
    [InlineData("0.01", 1886)]
    [InlineData("1850.75", 2020)]
    [InlineData("2500.00", 0)]
    [InlineData("9999999999999999.99", 2020)]
    public async Task Create_PersistsValidVehicleWithTrimmedOwner(string weightText, int year)
    {
        await using var db = Database.CreateContext();
        var manufacturer = await db.Manufacturers.SingleAsync(m => m.Name == "Toyota");
        var weight = decimal.Parse(weightText, CultureInfo.InvariantCulture);
        var expectedYear = year == 0 ? DateTime.UtcNow.Year : year;
        var page = new CreateModel(db, NullLogger<CreateModel>.Instance)
        {
            Input = new()
            {
                OwnerName = "  Jane Turei  ", ManufacturerId = manufacturer.Id,
                YearOfManufacture = expectedYear, WeightKg = weight
            }
        };

        var result = Assert.IsType<RedirectToPageResult>(await page.OnPostAsync());

        Assert.Equal("/Vehicles/Index", result.PageName);
        await using var verification = Database.CreateContext();
        var saved = Assert.Single(await verification.Vehicles.ToListAsync());
        Assert.Equal("Jane Turei", saved.OwnerName);
        Assert.Equal(manufacturer.Id, saved.ManufacturerId);
        Assert.Equal(expectedYear, saved.YearOfManufacture);
        Assert.Equal(weight, saved.WeightKg);
    }

    [SqlFact]
    public async Task Create_WithBindingErrorRedisplaysFormWithoutSaving()
    {
        await using var db = Database.CreateContext();
        var page = new CreateModel(db, NullLogger<CreateModel>.Instance);
        page.ModelState.AddModelError("Input.WeightKg", "The value 'abc' is not valid.");

        Assert.IsType<PageResult>(await page.OnPostAsync());
        Assert.Equal(5, page.Manufacturers.Count);
        Assert.Empty(await db.Vehicles.AsNoTracking().ToListAsync());
    }

    [SqlTheory]
    [InlineData("owner", false, "Amy,Ben,Zoe")]
    [InlineData("owner", true, "Zoe,Ben,Amy")]
    [InlineData("manufacturer", false, "Ben,Zoe,Amy")]
    [InlineData("manufacturer", true, "Amy,Zoe,Ben")]
    [InlineData("year", false, "Zoe,Ben,Amy")]
    [InlineData("year", true, "Amy,Ben,Zoe")]
    [InlineData("weight", false, "Amy,Zoe,Ben")]
    [InlineData("weight", true, "Ben,Zoe,Amy")]
    [InlineData("unknown", false, "Amy,Ben,Zoe")]
    public async Task Index_SortsUsingSqlServer(string sort, bool descending, string expected)
    {
        await using var db = Database.CreateContext();
        var manufacturers = await db.Manufacturers.ToDictionaryAsync(m => m.Name, m => m.Id);
        db.Vehicles.AddRange(
            new Vehicle { OwnerName = "Zoe", ManufacturerId = manufacturers["Mazda"], YearOfManufacture = 2000, WeightKg = 1000.10m },
            new Vehicle { OwnerName = "Amy", ManufacturerId = manufacturers["Toyota"], YearOfManufacture = 2022, WeightKg = 499.99m },
            new Vehicle { OwnerName = "Ben", ManufacturerId = manufacturers["Honda"], YearOfManufacture = 2010, WeightKg = 2500m });
        await db.SaveChangesAsync();

        var page = new IndexModel(db);
        await page.OnGetAsync(sort, descending);

        Assert.Equal(expected.Split(','), page.Vehicles.Select(v => v.OwnerName));
        Assert.Equal(sort == "unknown" ? "owner" : sort, page.SortBy);
        Assert.Equal(descending, page.Desc);
        Assert.All(page.Vehicles, vehicle => Assert.False(string.IsNullOrEmpty(vehicle.CategoryIconName)));
    }

    [SqlTheory]
    [InlineData("owner")]
    [InlineData("manufacturer")]
    [InlineData("year")]
    [InlineData("weight")]
    public async Task Index_PaginatesAfterSortingWithStableTies(string sort)
    {
        await using var db = Database.CreateContext();
        var manufacturerId = await db.Manufacturers.Select(m => m.Id).FirstAsync();
        // All primary sort values are tied. Weight uniquely identifies rows except in the weight case.
        var records = Enumerable.Range(1, 12).Select(i => new Vehicle
        {
            OwnerName = sort == "weight" ? $"Owner {i:00}" : "Same owner",
            ManufacturerId = manufacturerId, YearOfManufacture = 2020,
            WeightKg = sort == "weight" ? 1000m : 1000m + i
        }).ToList();
        db.Vehicles.AddRange(records);
        await db.SaveChangesAsync();
        var expected = records.OrderBy(v => v.Id).Skip(5).Take(5)
            .Select(v => (v.OwnerName, v.WeightKg)).ToList();

        var page = new IndexModel(db);
        await page.OnGetAsync(sort, desc: true, pageNumber: 2, pageSize: 5);

        Assert.Equal(12, page.TotalCount);
        Assert.Equal(3, page.TotalPages);
        Assert.Equal(2, page.PageNumber);
        Assert.Equal(expected, page.Vehicles.Select(v => (v.OwnerName, v.WeightKg)));
    }

    [SqlTheory]
    [InlineData(-5, 5, 1, 5, 5)]
    [InlineData(999, 5, 3, 5, 2)]
    [InlineData(1, -1, 1, 20, 12)]
    public async Task Index_NormalizesInvalidPagination(
        int requestedPage, int requestedSize, int expectedPage, int expectedSize, int expectedCount)
    {
        await using var db = Database.CreateContext();
        var manufacturerId = await db.Manufacturers.Select(m => m.Id).FirstAsync();
        db.Vehicles.AddRange(Enumerable.Range(1, 12).Select(i => new Vehicle
        {
            OwnerName = $"Owner {i:00}", ManufacturerId = manufacturerId,
            YearOfManufacture = 2020, WeightKg = 1000m
        }));
        await db.SaveChangesAsync();

        var page = new IndexModel(db);
        await page.OnGetAsync(pageNumber: requestedPage, pageSize: requestedSize);

        Assert.Equal(expectedPage, page.PageNumber);
        Assert.Equal(expectedSize, page.PageSize);
        Assert.Equal(expectedCount, page.Vehicles.Count);
    }

    [SqlFact]
    public async Task Index_HandlesEmptyDatabase()
    {
        await using var db = Database.CreateContext();
        var page = new IndexModel(db);
        await page.OnGetAsync(pageNumber: 999);

        Assert.Empty(page.Vehicles);
        Assert.Equal(0, page.TotalCount);
        Assert.Equal(1, page.PageNumber);
        Assert.Equal(1, page.TotalPages);
    }
}
