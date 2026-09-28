namespace CreditWorks.Web.Models;

public class Vehicle
{
    public int Id { get; set; }

    public string OwnerName { get; set; } = string.Empty;

    public int ManufacturerId { get; set; }

    public int YearOfManufacture { get; set; }

    public decimal WeightKg { get; set; }
}