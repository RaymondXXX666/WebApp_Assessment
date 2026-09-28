using CreditWorks.Web.Models;

namespace CreditWorks.Web.Services;

public static class WeightCategoryRules
{
    public static List<string> Validate(IEnumerable<VehicleCategory> categories)
    {
        var ordered = categories.OrderBy(c => c.MinWeightKg).ToList();
        var errors = new List<string>();

        if (ordered.Count == 0)
        {
            errors.Add("At least one category is required.");
            return errors;
        }

        if (ordered[0].MinWeightKg != 0m)
            errors.Add("The first category must start at 0 kg.");

        for (var i = 0; i < ordered.Count; i++)
        {
            var current = ordered[i];

            if (string.IsNullOrWhiteSpace(current.Name))
                errors.Add("Every category needs a name.");

            if (string.IsNullOrWhiteSpace(current.IconName))
                errors.Add("Every category needs an icon.");

            if (current.MinWeightKg < 0m)
                errors.Add($"{current.Name}: minimum weight cannot be negative.");

            if (current.MaxWeightKg is decimal max &&
                max <= current.MinWeightKg)
                errors.Add($"{current.Name}: maximum must exceed minimum.");

            if (i == ordered.Count - 1)
            {
                if (current.MaxWeightKg is not null)
                    errors.Add("The final category must have no upper limit.");

                continue;
            }

            if (current.MaxWeightKg is null)
            {
                errors.Add($"{current.Name}: only the final category may have no upper limit.");
            }
            else if (current.MaxWeightKg < ordered[i + 1].MinWeightKg)
            {
                errors.Add($"There is a gap after {current.Name}.");
            }
            else if (current.MaxWeightKg > ordered[i + 1].MinWeightKg)
            {
                errors.Add($"There is an overlap after {current.Name}.");
            }
        }

        return errors;
    }

    public static VehicleCategory Resolve(
        decimal weightKg,
        IEnumerable<VehicleCategory> categories)
    {
        if (weightKg <= 0m)
            throw new ArgumentOutOfRangeException(nameof(weightKg));

        var matches = categories
            .Where(c => weightKg >= c.MinWeightKg &&
                        (c.MaxWeightKg is null || weightKg < c.MaxWeightKg))
            .Take(2)
            .ToList();

        if (matches.Count != 1)
            throw new InvalidOperationException(
                "Vehicle weight must match exactly one category.");

        return matches[0];
    }
}
