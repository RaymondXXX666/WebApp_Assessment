using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Web.Services;

public static class SaveFailureMessages
{
    public static bool IsDatabaseFailure(Exception exception) =>
        exception is DbUpdateException || Unwrap(exception) is SqlException or TimeoutException;

    public static string Describe(Exception exception, bool saveAttempted, string reviewLocation)
    {
        var databaseException = Unwrap(exception);
        var sqlException = databaseException as SqlException;

        if (databaseException is DbUpdateConcurrencyException || sqlException?.Number is 547 or 2601 or 2627)
            return "Related data has changed. Review the latest data before trying again. Your input has been kept.";

        // SQL Server rolls back the transaction chosen as a deadlock victim.
        if (sqlException?.Number == 1205)
            return "Your changes were not saved because another operation was using the same data. Please try again.";

        if (!saveAttempted)
            return "The data could not be loaded, so nothing was saved. Please try again later. Your input has been kept.";

        // A lost connection or timeout can occur after the server commits. Never retry automatically.
        return $"We could not confirm whether your changes were saved. Check {reviewLocation} before submitting again to avoid duplicate changes. Your input has been kept.";
    }

    private static Exception Unwrap(Exception exception)
    {
        // EF's non-retrying SQL Server strategy wraps transient failures in InvalidOperationException.
        while (exception is InvalidOperationException or DbUpdateException &&
               exception is not DbUpdateConcurrencyException && exception.InnerException is { } inner)
            exception = inner;

        return exception;
    }
}
