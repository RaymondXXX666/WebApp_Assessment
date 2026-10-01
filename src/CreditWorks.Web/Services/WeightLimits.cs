namespace CreditWorks.Web.Services;

public static class WeightLimits
{
    // Matches the decimal(18, 2) columns used for vehicles and category boundaries.
    public const decimal MaximumKg = 9999999999999999.99m;
}
