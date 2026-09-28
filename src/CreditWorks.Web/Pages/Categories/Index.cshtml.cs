using CreditWorks.Web.Data;
using CreditWorks.Web.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Pages.Categories;

public class IndexModel(
    AppDbContext db,
    CategoryConfigurationService categoryService) : PageModel
{
    [BindProperty]
    public List<CategoryDraft> Categories { get; set; } = [];

    public async Task OnGetAsync()
    {
        Categories = await db.VehicleCategories
            .AsNoTracking()
            .OrderBy(category => category.MinWeightKg)
            .Select(category => new CategoryDraft
            {
                Id = category.Id,
                Name = category.Name,
                IconName = category.IconName,
                MinWeightKg = category.MinWeightKg,
                MaxWeightKg = category.MaxWeightKg
            })
            .ToListAsync();
    }

    public async Task<IActionResult> OnPostAsync()
    {
        if (!ModelState.IsValid)
            return Page();

        var errors = await categoryService.SaveAsync(
            Categories, HttpContext.RequestAborted);

        if (errors.Count > 0)
        {
            foreach (var error in errors)
                ModelState.AddModelError(string.Empty, error);

            return Page();
        }

        TempData["SuccessMessage"] = "Categories saved.";
        return RedirectToPage();
    }
}
