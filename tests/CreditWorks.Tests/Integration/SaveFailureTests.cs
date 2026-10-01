using System.Data.Common;
using CreditWorks.Web.Pages.Vehicles;
using CreditWorks.Web.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Logging;
using CategoryPage = CreditWorks.Web.Pages.Categories.IndexModel;

namespace CreditWorks.Tests.Integration;

public class SaveFailureTests(SqlServerFixture database) : SqlServerTest(database)
{
    [SqlFact]
    public async Task Vehicle_FailedInitialReadKeepsInputAndDoesNotAttemptSaving()
    {
        var failure = new FailedConnection();
        await using var db = Database.CreateContext(failure);
        var logger = new RecordingLogger<CreateModel>();
        var page = VehiclePage(db, logger, 123);

        Assert.IsType<PageResult>(await page.OnPostAsync());

        Assert.Contains("nothing was saved", Errors(page));
        Assert.Equal("Jane Turei", page.Input.OwnerName);
        Assert.Equal(1850.75m, page.Input.WeightKg);
        Assert.Equal(2020, page.Input.YearOfManufacture);
        Assert.Equal("123", Assert.Single(page.Manufacturers).Value);
        Assert.Equal(1, failure.Attempts);
        Assert.Single(logger.Exceptions);
        Assert.DoesNotContain("private", Errors(page));
        await using var verification = Database.CreateContext();
        Assert.Empty(await verification.Vehicles.ToListAsync());
    }

    [SqlTheory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task Vehicle_SaveTimeoutPreservesFormAndNeverRetries(bool afterWrite)
    {
        await using var setup = Database.CreateContext();
        var manufacturerId = await setup.Manufacturers.Select(m => m.Id).FirstAsync();
        var failure = new SaveTimeout(afterWrite);
        await using var db = Database.CreateContext(failure);
        var logger = new RecordingLogger<CreateModel>();
        var page = VehiclePage(db, logger, manufacturerId);

        Assert.IsType<PageResult>(await page.OnPostAsync());

        Assert.Contains("could not confirm", Errors(page));
        Assert.Contains("Check the vehicle list", Errors(page));
        Assert.Equal("Jane Turei", page.Input.OwnerName);
        Assert.Equal(manufacturerId, page.Input.ManufacturerId);
        Assert.Equal(1850.75m, page.Input.WeightKg);
        Assert.Equal(5, page.Manufacturers.Count);
        Assert.Equal(1, failure.Attempts);
        Assert.Single(logger.Exceptions);
        Assert.DoesNotContain("private", Errors(page));
        await using var verification = Database.CreateContext();
        // SavedChanges runs after EF's implicit commit: an error response can still mean a saved vehicle.
        Assert.Equal(afterWrite ? 1 : 0, await verification.Vehicles.CountAsync());
    }

    [SqlFact]
    public async Task Vehicle_ManufacturerDeletedDuringSaveShowsChangedDataMessage()
    {
        await using var setup = Database.CreateContext();
        var manufacturerId = await setup.Manufacturers.Select(m => m.Id).FirstAsync();
        await using var db = Database.CreateContext(new DeleteManufacturerBeforeSave(Database, manufacturerId));
        var logger = new RecordingLogger<CreateModel>();
        var page = VehiclePage(db, logger, manufacturerId);

        Assert.IsType<PageResult>(await page.OnPostAsync());

        Assert.Contains("Related data has changed", Errors(page));
        Assert.Equal("Jane Turei", page.Input.OwnerName);
        Assert.IsType<DbUpdateException>(Assert.Single(logger.Exceptions));
        await using var verification = Database.CreateContext();
        Assert.Empty(await verification.Vehicles.ToListAsync());
    }

    [SqlFact]
    public async Task Categories_SaveTimeoutKeepsDraftsAndRollsBackWrites()
    {
        await using var setup = Database.CreateContext();
        var original = await setup.VehicleCategories.AsNoTracking().OrderBy(c => c.MinWeightKg).ToListAsync();
        var failure = new SaveTimeout(afterWrite: true);
        await using var db = Database.CreateContext(failure);
        var logger = new RecordingLogger<CategoryPage>();
        var page = new CategoryPage(db, new CategoryConfigurationService(db), logger)
        {
            PageContext = new PageContext { HttpContext = new DefaultHttpContext() },
            Categories = original.Select(c => new CategoryDraft
            {
                Id = c.Id, Name = c.Name, IconName = c.IconName,
                MinWeightKg = c.MinWeightKg, MaxWeightKg = c.MaxWeightKg
            }).ToList()
        };
        page.Categories[0].Name = "Edited name";
        page.Categories[1].MaxWeightKg = 2000m;
        page.Categories[2].MinWeightKg = 2000m;

        Assert.IsType<PageResult>(await page.OnPostAsync());

        Assert.Contains("Check the current category configuration", Errors(page));
        Assert.Equal("Edited name", page.Categories[0].Name);
        Assert.Equal(2000m, page.Categories[1].MaxWeightKg);
        Assert.Equal(1, failure.Attempts);
        Assert.Single(logger.Exceptions);
        Assert.DoesNotContain("private", Errors(page));
        await using var verification = Database.CreateContext();
        var persisted = await verification.VehicleCategories.OrderBy(c => c.MinWeightKg).ToListAsync();
        Assert.Equal(original.Select(c => (c.Id, c.Name, c.MinWeightKg, c.MaxWeightKg)),
            persisted.Select(c => (c.Id, c.Name, c.MinWeightKg, c.MaxWeightKg)));
    }

    private static CreateModel VehiclePage(
        CreditWorks.Web.Data.AppDbContext db, ILogger<CreateModel> logger, int manufacturerId) => new(db, logger)
    {
        Input = new()
        {
            OwnerName = "Jane Turei", ManufacturerId = manufacturerId,
            YearOfManufacture = 2020, WeightKg = 1850.75m
        }
    };

    private static string Errors(PageModel page) => string.Join(" ",
        page.ModelState.Values.SelectMany(v => v.Errors).Select(e => e.ErrorMessage));

    private sealed class FailedConnection : DbConnectionInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult> ConnectionOpeningAsync(
            DbConnection connection, ConnectionEventData eventData, InterceptionResult result,
            CancellationToken cancellationToken = default)
        {
            Attempts++;
            throw new TimeoutException("private connection details");
        }
    }

    private sealed class SaveTimeout(bool afterWrite) : SaveChangesInterceptor
    {
        public int Attempts { get; private set; }

        public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            Attempts++;
            if (!afterWrite) throw new TimeoutException("private connection details");
            return base.SavingChangesAsync(eventData, result, cancellationToken);
        }

        public override ValueTask<int> SavedChangesAsync(
            SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
        {
            if (afterWrite) throw new TimeoutException("private connection details");
            return base.SavedChangesAsync(eventData, result, cancellationToken);
        }
    }

    private sealed class DeleteManufacturerBeforeSave(SqlServerFixture database, int manufacturerId) : SaveChangesInterceptor
    {
        public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(
            DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
        {
            await using var otherRequest = database.CreateContext();
            await otherRequest.Manufacturers.Where(m => m.Id == manufacturerId).ExecuteDeleteAsync(cancellationToken);
            return result;
        }
    }

    private sealed class RecordingLogger<T> : ILogger<T>
    {
        public List<Exception> Exceptions { get; } = [];
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
        public bool IsEnabled(LogLevel logLevel) => true;
        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state,
            Exception? exception, Func<TState, Exception?, string> formatter)
        {
            if (exception is not null) Exceptions.Add(exception);
        }
    }
}
