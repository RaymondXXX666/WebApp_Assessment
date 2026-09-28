namespace CreditWorks.Web.Models;

public class VehicleCategory
{
    public int Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string IconName { get; set; } = string.Empty;

    public decimal MinWeightKg { get; set; }

    public decimal? MaxWeightKg { get; set; }
}
