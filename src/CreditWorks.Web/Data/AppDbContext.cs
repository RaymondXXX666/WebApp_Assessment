using CreditWorks.Web.Models;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options)
{
    public DbSet<Vehicle> Vehicles => Set<Vehicle>();
    public DbSet<Manufacturer> Manufacturers => Set<Manufacturer>();
    public DbSet<VehicleCategory> VehicleCategories => Set<VehicleCategory>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Vehicle>(vehicle =>
        {
            vehicle.Property(v => v.OwnerName)
                .HasMaxLength(150)
                .IsRequired();

            vehicle.Property(v => v.WeightKg)
                .HasPrecision(18, 2);

            vehicle.HasOne<Manufacturer>()
                .WithMany()
                .HasForeignKey(v => v.ManufacturerId)
                .OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<Manufacturer>(manufacturer =>
        {
            manufacturer.Property(m => m.Name)
                .HasMaxLength(100)
                .IsRequired();

            manufacturer.HasIndex(m => m.Name)
                .IsUnique();
        });

        modelBuilder.Entity<VehicleCategory>(category =>
        {
            category.Property(c => c.Name)
                .HasMaxLength(100)
                .IsRequired();

            category.Property(c => c.IconName)
                .HasMaxLength(100)
                .IsRequired();

            category.Property(c => c.MinWeightKg)
                .HasPrecision(18, 2);

            category.Property(c => c.MaxWeightKg)
                .HasPrecision(18, 2);
        });
    }
}
