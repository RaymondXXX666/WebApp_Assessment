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
    public int PageNumber { get; private set; } = 1;
    public int PageSize { get; private set; } = 20;
    public int TotalCount { get; private set; }
    public int TotalPages { get; private set; } = 1;


    public async Task OnGetAsync(
        string sort = "owner", bool desc = false,
        int pageNumber = 1, int pageSize = 20,
        CancellationToken cancellationToken = default)
    {
        SortBy = sort is "manufacturer" or "year" or "weight"
            ? sort
            : "owner";
        Desc = desc;
        PageSize = pageSize is 5 or 20 or 50 ? pageSize : 20;

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

        TotalCount = await query.CountAsync(cancellationToken);
        TotalPages = Math.Max(1, (int)Math.Ceiling((double)TotalCount / PageSize));
        PageNumber = Math.Clamp(pageNumber, 1, TotalPages);

        var records = await ordered
            .Skip((PageNumber - 1) * PageSize)
            .Take(PageSize)
            .ToListAsync(cancellationToken);

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
