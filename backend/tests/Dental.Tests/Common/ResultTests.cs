using Dental.Application.Common;
using Xunit;

namespace Dental.Tests.Common;

public sealed class ResultTests
{
    [Fact]
    public void Success_ShouldHaveIsSuccessTrue()
    {
        var result = Result<string>.Success("hello");
        Assert.True(result.IsSuccess);
        Assert.False(result.IsFailure);
        Assert.Equal("hello", result.Value);
        Assert.Equal(Error.None, result.Error);
    }

    [Fact]
    public void Failure_ShouldHaveIsFailureTrue()
    {
        var result = Result<string>.Failure(Error.NotFound);
        Assert.False(result.IsSuccess);
        Assert.True(result.IsFailure);
        Assert.Null(result.Value);
        Assert.Equal("GEN_001", result.Error.Code);
    }

    [Fact]
    public void VoidResult_Success_Works()
    {
        var result = Result.Success();
        Assert.True(result.IsSuccess);
    }

    [Fact]
    public void VoidResult_Failure_Works()
    {
        var result = Result.Failure(Error.ServerError);
        Assert.True(result.IsFailure);
        Assert.Equal("GEN_500", result.Error.Code);
    }
}
