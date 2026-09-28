using CreditWorks.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Data;

public static class DatabaseSeeder
{
    public static void Seed(DbContext context)
    {
        if (!context.Set<Manufacturer>().Any())
        {
            context.Set<Manufacturer>().AddRange(
                new Manufacturer { Name = "Mazda" },
                new Manufacturer { Name = "Mercedes" },
                new Manufacturer { Name = "Honda" },
                new Manufacturer { Name = "Ferrari" },
                new Manufacturer { Name = "Toyota" });
        }

        if (!context.Set<VehicleCategory>().Any())
        {
            context.Set<VehicleCategory>().AddRange(
                new VehicleCategory
                {
                    Name = "Light",
                    IconName = "light.svg",
                    MinWeightKg = 0m,
                    MaxWeightKg = 500m
                },
                new VehicleCategory
                {
                    Name = "Medium",
                    IconName = "medium.svg",
                    MinWeightKg = 500m,
                    MaxWeightKg = 2500m
                },
                new VehicleCategory
                {
                    Name = "Heavy",
                    IconName = "heavy.svg",
                    MinWeightKg = 2500m,
                    MaxWeightKg = null
                });
        }

        context.SaveChanges();
    }
}
