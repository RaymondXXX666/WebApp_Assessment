using System.ComponentModel.DataAnnotations;
using System.Globalization;
using CreditWorks.Web.Data;
using CreditWorks.Web.Models;
using CreditWorks.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Pages.Vehicles;

public class CreateModel(AppDbContext db, ILogger<CreateModel> logger) : PageModel
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
            else if (weight > WeightLimits.MaximumKg)
                ModelState.AddModelError(
                    "Input.WeightKg",
                    $"Weight cannot exceed {WeightLimits.MaximumKg.ToString("0.00", CultureInfo.InvariantCulture)} kg.");
            else if (decimal.Round(weight, 2) != weight)
                ModelState.AddModelError(
                    "Input.WeightKg",
                    "Weight can have at most two decimal places.");
        }

        var saveAttempted = false;
        try
        {
            // Keep these options available if saving fails; do not query a failed database again.
            await LoadManufacturersAsync();
            if (Input.ManufacturerId is int manufacturerId &&
                !Manufacturers.Any(m => m.Value == manufacturerId.ToString()))
            {
                ModelState.AddModelError(
                    "Input.ManufacturerId",
                    "Select a valid manufacturer. The manufacturer list may have changed.");
            }

            if (!ModelState.IsValid)
                return Page();

            db.Vehicles.Add(new Vehicle
            {
                OwnerName = Input.OwnerName.Trim(),
                ManufacturerId = Input.ManufacturerId!.Value,
                YearOfManufacture = Input.YearOfManufacture!.Value,
                WeightKg = Input.WeightKg!.Value
            });

            saveAttempted = true;
            await db.SaveChangesAsync();
            return RedirectToPage("/Vehicles/Index");
        }
        catch (Exception exception) when (SaveFailureMessages.IsDatabaseFailure(exception))
        {
            logger.LogWarning(exception, "Vehicle submission failed. Save attempted: {SaveAttempted}", saveAttempted);
            ModelState.AddModelError(string.Empty,
                SaveFailureMessages.Describe(exception, saveAttempted, "the vehicle list"));

            if (Manufacturers.Count == 0 && Input.ManufacturerId is int selectedId)
            {
                // Retain the submitted selection even if loading its display name was impossible.
                Manufacturers.Add(new SelectListItem("Previously selected manufacturer (temporarily unavailable)",
                    selectedId.ToString()));
            }

            return Page();
        }
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
