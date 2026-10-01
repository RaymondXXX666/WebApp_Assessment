using CreditWorks.Web.Data;
using CreditWorks.Web.Models;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Endpoints;

public static class ManufacturerEndpoints
{
    public static void MapManufacturerEndpoints(this WebApplication app)
    {
        var group = app.MapGroup("/api/manufacturers");

        group.MapGet("", async (AppDbContext db, CancellationToken ct) =>
            Results.Ok(await db.Manufacturers.AsNoTracking()
                .OrderBy(m => m.Name).ToListAsync(ct)));

        group.MapGet("/{id:int}", GetById);

        group.MapPost("", (ManufacturerInput input, AppDbContext db,
            CancellationToken ct) => SaveName(null, input, db, ct));

        group.MapPut("/{id:int}", (int id, ManufacturerInput input,
            AppDbContext db, CancellationToken ct) =>
            SaveName(id, input, db, ct));

        group.MapDelete("/{id:int}", Delete);
    }

    private static async Task<IResult> GetById(
        int id, AppDbContext db, CancellationToken ct)
    {
        var manufacturer = await db.Manufacturers.AsNoTracking()
            .SingleOrDefaultAsync(m => m.Id == id, ct);

        return manufacturer is null
            ? Results.NotFound()
            : Results.Ok(manufacturer);
    }

    private static async Task<IResult> SaveName(
        int? id, ManufacturerInput input, AppDbContext db,
        CancellationToken ct)
    {
        var name = input.Name?.Trim();

        if (string.IsNullOrEmpty(name) || name.Length > 100)
        {
            return Results.ValidationProblem(
                new Dictionary<string, string[]>
                {
                    ["name"] = ["Enter a name of 1 to 100 characters."]
                });
        }

        var manufacturer = id.HasValue
            ? await db.Manufacturers.SingleOrDefaultAsync(
                m => m.Id == id.Value, ct)
            : new Manufacturer();

        if (manufacturer is null)
            return Results.NotFound();

        if (await db.Manufacturers.AnyAsync(
            m => m.Name == name && m.Id != manufacturer.Id, ct))
        {
            return DuplicateName();
        }

        manufacturer.Name = name;
        if (!id.HasValue)
            db.Manufacturers.Add(manufacturer);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 2601 or 2627 })
        {
            return DuplicateName();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.NotFound();
        }

        return id.HasValue
            ? Results.Ok(manufacturer)
            : Results.Created(
                $"/api/manufacturers/{manufacturer.Id}", manufacturer);
    }

    private static async Task<IResult> Delete(
        int id, AppDbContext db, CancellationToken ct)
    {
        var manufacturer = await db.Manufacturers
            .SingleOrDefaultAsync(m => m.Id == id, ct);

        if (manufacturer is null)
            return Results.NotFound();

        if (await db.Vehicles.AnyAsync(v => v.ManufacturerId == id, ct))
            return ManufacturerInUse();

        db.Manufacturers.Remove(manufacturer);

        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException ex) when (
            ex.InnerException is SqlException { Number: 547 })
        {
            return ManufacturerInUse();
        }
        catch (DbUpdateConcurrencyException)
        {
            return Results.NotFound();
        }

        return Results.NoContent();
    }

    private static IResult DuplicateName() =>
        Results.Conflict(new { error = "A manufacturer with this name already exists." });

    private static IResult ManufacturerInUse() =>
        Results.Conflict(new { error = "This manufacturer is referenced by vehicles." });

    public sealed record ManufacturerInput(string? Name);
}
