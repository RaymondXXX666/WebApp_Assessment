using CreditWorks.Web.Data;
using CreditWorks.Web.Services;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Pages.Vehicles;

public class IndexModel(AppDbContext db) : PageModel
{
    public List<VehicleRow> Vehicles { get; private set; } = [];
    public string SortBy { get; private set; } = "owner";
    public bool Desc { get; private set; }

    public async Task OnGetAsync(string sort = "owner", bool desc = false)
    {
        SortBy = sort is "manufacturer" or "year" or "weight"
            ? sort
            : "owner";
        Desc = desc;

        var categories = await db.VehicleCategories
            .AsNoTracking()
            .ToListAsync();

        var query =
            from vehicle in db.Vehicles.AsNoTracking()
            join manufacturer in db.Manufacturers.AsNoTracking()
                on vehicle.ManufacturerId equals manufacturer.Id
            select new
            {
                vehicle.Id,
                vehicle.OwnerName,
                Manufacturer = manufacturer.Name,
                vehicle.YearOfManufacture,
                vehicle.WeightKg
            };

        var ordered = (SortBy, Desc) switch
        {
            ("owner", true) => query.OrderByDescending(v => v.OwnerName)
                .ThenBy(v => v.Id),
            ("manufacturer", false) => query.OrderBy(v => v.Manufacturer)
                .ThenBy(v => v.Id),
            ("manufacturer", true) => query.OrderByDescending(v => v.Manufacturer)
                .ThenBy(v => v.Id),
            ("year", false) => query.OrderBy(v => v.YearOfManufacture)
                .ThenBy(v => v.Id),
            ("year", true) => query.OrderByDescending(v => v.YearOfManufacture)
                .ThenBy(v => v.Id),
            ("weight", false) => query.OrderBy(v => v.WeightKg)
                .ThenBy(v => v.Id),
            ("weight", true) => query.OrderByDescending(v => v.WeightKg)
                .ThenBy(v => v.Id),
            _ => query.OrderBy(v => v.OwnerName).ThenBy(v => v.Id)
        };

        var records = await ordered.ToListAsync();

        Vehicles = records.Select(record =>
        {
            var category = WeightCategoryRules.Resolve(record.WeightKg, categories);

            return new VehicleRow(
                record.OwnerName,
                record.Manufacturer,
                record.YearOfManufacture,
                record.WeightKg,
                category.Name,
                category.IconName);
        }).ToList();
    }

    public record VehicleRow(
        string OwnerName,
        string Manufacturer,
        int YearOfManufacture,
        decimal WeightKg,
        string CategoryName,
        string CategoryIconName);
}
