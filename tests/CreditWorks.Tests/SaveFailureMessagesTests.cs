using CreditWorks.Web.Services;
using Microsoft.EntityFrameworkCore;

namespace CreditWorks.Tests;

public class SaveFailureMessagesTests
{
    [Fact]
    public void FailureBeforeSaving_SaysNothingWasSavedWithoutExposingDetails()
    {
        var exception = new TimeoutException("Private connection details");
        Assert.True(SaveFailureMessages.IsDatabaseFailure(exception));

        var message = SaveFailureMessages.Describe(exception, false, "the vehicle list");

        Assert.Contains("nothing was saved", message);
        Assert.DoesNotContain(exception.Message, message);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void FailureDuringSaving_RequestsVerificationInsteadOfPromisingFailure(bool wrapped)
    {
        var exception = wrapped
            ? (Exception)new DbUpdateException("Private database details", new TimeoutException())
            : new TimeoutException("Private connection details");

        Assert.True(SaveFailureMessages.IsDatabaseFailure(exception));
        var message = SaveFailureMessages.Describe(exception, true, "the vehicle list");

        Assert.Contains("could not confirm", message);
        Assert.Contains("Check the vehicle list", message);
        Assert.DoesNotContain("nothing was saved", message);
        Assert.DoesNotContain(exception.Message, message);
    }

    [Fact]
    public void ConcurrencyFailure_RequestsReviewOfChangedData()
    {
        var exception = new DbUpdateConcurrencyException("Private record details");
        Assert.True(SaveFailureMessages.IsDatabaseFailure(exception));

        var message = SaveFailureMessages.Describe(exception, true, "the current category configuration");

        Assert.Contains("Related data has changed", message);
        Assert.DoesNotContain(exception.Message, message);
    }

    [Fact]
    public void EfWrappedTimeout_IsStillHandledAsADatabaseFailure()
    {
        var exception = new InvalidOperationException("Execution strategy details",
            new DbUpdateException("Database details", new TimeoutException("Connection details")));

        Assert.True(SaveFailureMessages.IsDatabaseFailure(exception));
        Assert.Contains("nothing was saved", SaveFailureMessages.Describe(exception, false, "the vehicle list"));
        Assert.Contains("could not confirm", SaveFailureMessages.Describe(exception, true, "the vehicle list"));
    }

    [Fact]
    public void UnexpectedErrorsAndRequestCancellation_AreNotTreatedAsDatabaseFailures()
    {
        Assert.False(SaveFailureMessages.IsDatabaseFailure(new InvalidOperationException()));
        Assert.False(SaveFailureMessages.IsDatabaseFailure(new OperationCanceledException()));
    }
}
