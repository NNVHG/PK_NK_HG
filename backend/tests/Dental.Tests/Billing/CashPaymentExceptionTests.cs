using System.Reflection;
using Dental.Infrastructure.Repositories;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using Xunit;

namespace Dental.Tests.Billing;

public sealed class CashPaymentExceptionTests
{
    [Theory]
    [InlineData("40001", 0)] [InlineData("40001", 1)] [InlineData("40001", 2)]
    [InlineData("40P01", 0)] [InlineData("40P01", 1)] [InlineData("40P01", 2)]
    [InlineData("23505", 0)] [InlineData("23505", 1)] [InlineData("23505", 2)]
    public void RecognizesKnownStatesAtEveryWrapperDepth(string state, int depth)
    {
        Exception error = new PostgresException("Synthetic failure", "ERROR", "ERROR", state);
        if (depth >= 1) error = new DbUpdateException("Synthetic wrapper", error);
        if (depth >= 2) error = new InvalidOperationException("Synthetic wrapper", error);
        Assert.True(Classify(error));
    }

    [Theory]
    [InlineData("23503")] [InlineData("08006")] [InlineData("57014")]
    public void UnknownDatabaseStatesAreNotConvertedToConcurrency(string state)
        => Assert.False(Classify(new InvalidOperationException("Synthetic wrapper", new PostgresException("Synthetic", "ERROR", "ERROR", state))));

    [Fact]
    public void TextAndCancellationAreNotSqlStateEvidence()
    {
        Assert.False(Classify(new InvalidOperationException("40001")));
        Assert.False(Classify(new OperationCanceledException()));
    }

    private static bool Classify(Exception error) => (bool)typeof(CashPaymentRepository)
        .GetMethod("IsConcurrentFailure", BindingFlags.NonPublic | BindingFlags.Static)!.Invoke(null, [error])!;
}
