using System.Data;
using CreditWorks.Web.Data;
using CreditWorks.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Services;

public class CategoryConfigurationService(AppDbContext db)
{
    private static readonly HashSet<string> AllowedIcons =
        new(StringComparer.Ordinal)
        {
            "light.svg", "medium.svg", "heavy.svg"
        };

    private const decimal MaxStoredWeight = 9999999999999999.99m;

    public async Task<List<string>> SaveAsync(
        IReadOnlyList<CategoryDraft> drafts,
        CancellationToken cancellationToken = default)
    {
        var errors = ValidateDrafts(drafts);
        if (errors.Count > 0)
            return errors;

        var proposed = drafts.Select(draft => new VehicleCategory
        {
            Id = draft.Id,
            Name = draft.Name.Trim(),
            IconName = draft.IconName,
            MinWeightKg = draft.MinWeightKg!.Value,
            MaxWeightKg = draft.MaxWeightKg
        }).ToList();

        errors.AddRange(WeightCategoryRules.Validate(proposed));
        if (errors.Count > 0)
            return errors;

        await using var transaction = await db.Database.BeginTransactionAsync(
            IsolationLevel.Serializable, cancellationToken);

        var existing = await db.VehicleCategories.ToListAsync(cancellationToken);
        var existingById = existing.ToDictionary(category => category.Id);

        if (drafts.Any(draft => draft.Id > 0 &&
                                !existingById.ContainsKey(draft.Id)))
        {
            return ["A category has changed. Reload the page and try again."];
        }

        var submittedIds = drafts
            .Where(draft => draft.Id > 0)
            .Select(draft => draft.Id)
            .ToHashSet();

        db.VehicleCategories.RemoveRange(
            existing.Where(category => !submittedIds.Contains(category.Id)));

        foreach (var draft in drafts)
        {
            if (draft.Id == 0)
            {
                db.VehicleCategories.Add(new VehicleCategory
                {
                    Name = draft.Name.Trim(),
                    IconName = draft.IconName,
                    MinWeightKg = draft.MinWeightKg!.Value,
                    MaxWeightKg = draft.MaxWeightKg
                });
            }
            else
            {
                var category = existingById[draft.Id];
                category.Name = draft.Name.Trim();
                category.IconName = draft.IconName;
                category.MinWeightKg = draft.MinWeightKg!.Value;
                category.MaxWeightKg = draft.MaxWeightKg;
            }
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return [];
    }

    private static List<string> ValidateDrafts(
        IReadOnlyList<CategoryDraft> drafts)
    {
        var errors = new List<string>();

        if (drafts.Count == 0)
            errors.Add("At least one category is required.");

        if (drafts.Any(draft => draft.Id < 0))
            errors.Add("Category IDs cannot be negative.");

        if (drafts.Where(draft => draft.Id > 0)
            .GroupBy(draft => draft.Id)
            .Any(group => group.Count() > 1))
        {
            errors.Add("A category cannot appear twice.");
        }

        if (drafts.Any(draft => string.IsNullOrWhiteSpace(draft.Name) ||
                                draft.Name.Length > 100))
        {
            errors.Add("Each category needs a name of at most 100 characters.");
        }
        else if (drafts.GroupBy(
                draft => draft.Name.Trim(),
                StringComparer.OrdinalIgnoreCase)
            .Any(group => group.Count() > 1))
        {
            errors.Add("Category names must be unique.");
        }

        if (drafts.Any(draft => !AllowedIcons.Contains(draft.IconName)))
            errors.Add("Select one of the available icons.");

        if (drafts.Any(draft => draft.MinWeightKg is null))
            errors.Add("Every category needs a minimum weight.");

        foreach (var draft in drafts)
        {
            if (draft.MinWeightKg is decimal min &&
                (min < 0m || min > MaxStoredWeight ||
                 decimal.Round(min, 2) != min))
            {
                errors.Add("Minimum weights must be non-negative with at most two decimal places.");
            }

            if (draft.MaxWeightKg is decimal max &&
                (max > MaxStoredWeight || decimal.Round(max, 2) != max))
            {
                errors.Add("Maximum weights must have at most two decimal places.");
            }
        }

        return errors;
    }
}
