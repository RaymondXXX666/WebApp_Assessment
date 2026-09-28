using System.ComponentModel.DataAnnotations;
using CreditWorks.Web.Data;
using CreditWorks.Web.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Pages.Vehicles;

public class CreateModel(AppDbContext db) : PageModel
{
    [BindProperty]
    public VehicleInput Input { get; set; } = new();

    public List<SelectListItem> Manufacturers { get; private set; } = [];

    public async Task OnGetAsync() => await LoadManufacturersAsync();

    public async Task<IActionResult> OnPostAsync()
    {
        if (Input.YearOfManufacture is int year &&
            (year < 1886 || year > DateTime.UtcNow.Year))
        {
            ModelState.AddModelError(
                "Input.YearOfManufacture",
                "Enter a year between 1886 and the current year.");
        }

        if (Input.WeightKg is decimal weight)
        {
            if (weight <= 0m)
                ModelState.AddModelError("Input.WeightKg", "Weight must be positive.");
            else if (decimal.Round(weight, 2) != weight)
                ModelState.AddModelError(
                    "Input.WeightKg",
                    "Weight can have at most two decimal places.");
        }

        if (Input.ManufacturerId is int manufacturerId &&
            !await db.Manufacturers.AnyAsync(m => m.Id == manufacturerId))
        {
            ModelState.AddModelError(
                "Input.ManufacturerId",
                "Select a valid manufacturer.");
        }

        if (!ModelState.IsValid)
        {
            await LoadManufacturersAsync();
            return Page();
        }

        db.Vehicles.Add(new Vehicle
        {
            OwnerName = Input.OwnerName.Trim(),
            ManufacturerId = Input.ManufacturerId!.Value,
            YearOfManufacture = Input.YearOfManufacture!.Value,
            WeightKg = Input.WeightKg!.Value
        });

        await db.SaveChangesAsync();
        return RedirectToPage("/Vehicles/Index");
    }

    private async Task LoadManufacturersAsync()
    {
        var manufacturers = await db.Manufacturers
            .AsNoTracking()
            .OrderBy(m => m.Name)
            .ToListAsync();

        Manufacturers = manufacturers
            .Select(m => new SelectListItem(m.Name, m.Id.ToString()))
            .ToList();
    }

    public class VehicleInput
    {
        [Required(ErrorMessage = "Owner's name is required.")]
        [StringLength(150)]
        public string OwnerName { get; set; } = string.Empty;

        [Required(ErrorMessage = "Select a manufacturer.")]
        public int? ManufacturerId { get; set; }

        [Required(ErrorMessage = "Year is required.")]
        public int? YearOfManufacture { get; set; }

        [Required(ErrorMessage = "Weight is required.")]
        public decimal? WeightKg { get; set; }
    }
}
