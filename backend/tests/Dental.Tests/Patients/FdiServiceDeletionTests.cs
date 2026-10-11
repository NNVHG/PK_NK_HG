using Dental.Application.Common;
using Dental.Application.Features.MOD_FDI.DTOs;
using Dental.Application.Features.MOD_FDI.Services;
using Dental.Application.Features.MOD_FDI.Validators;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Patients;
public sealed class FdiServiceDeletionTests
{
    [Theory]
    [InlineData(RoleCodes.Receptionist, 1)] [InlineData(RoleCodes.Patient, 1)]
    [InlineData(RoleCodes.Assistant, 1)] [InlineData(null, 1)] [InlineData(RoleCodes.Admin, 0)]
    public async Task UnauthorizedActorNeverCallsRepository(string? role, int actor)
    {
        var repo = Substitute.For<IFdiServiceDeletionRepository>();
        var result = await new FdiServiceDeletionService(repo, new()).DeleteAsync(new(1, 2, "Điều chỉnh"), actor, role, null);
        Assert.Equal(Error.Forbidden, result.Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Theory]
    [InlineData(0, 2, "Lý do")] [InlineData(1, 0, "Lý do")] [InlineData(1, 2, "")]
    [InlineData(1, 2, "   ")] [InlineData(1, 2, null)]
    public async Task InvalidRequestNeverCallsRepository(int visit, int assignment, string? reason)
    {
        var repo = Substitute.For<IFdiServiceDeletionRepository>();
        var result = await new FdiServiceDeletionService(repo, new()).DeleteAsync(new(visit, assignment, reason!), 1, RoleCodes.Admin, null);
        Assert.Equal(Error.Validation, result.Error); Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public void ReasonLengthIsBounded() => Assert.False(new DeleteVisitServiceRequestValidator().Validate(new DeleteVisitServiceRequest(1, 2, new string('x', 501))).IsValid);
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Dentist)]
    public async Task AllowedRolePreservesIdentityAndTrimsReason(string role)
    {
        var repo = Substitute.For<IFdiServiceDeletionRepository>();
        repo.DeleteAsync(Arg.Any<DeleteVisitServiceRequest>(), 3, role, "127.0.0.1", Arg.Any<CancellationToken>()).Returns(Result.Success());
        var result = await new FdiServiceDeletionService(repo, new()).DeleteAsync(new(1, 2, "  Điều chỉnh  "), 3, role, "127.0.0.1");
        Assert.True(result.IsSuccess);
        await repo.Received(1).DeleteAsync(new(1, 2, "Điều chỉnh"), 3, role, "127.0.0.1", Arg.Any<CancellationToken>());
    }
    [Fact] public async Task RepositoryFailurePropagates()
    {
        var repo = Substitute.For<IFdiServiceDeletionRepository>();
        repo.DeleteAsync(Arg.Any<DeleteVisitServiceRequest>(), 1, RoleCodes.Admin, null, Arg.Any<CancellationToken>()).Returns(Result.Failure(Error.Conflict));
        Assert.Equal(Error.Conflict, (await new FdiServiceDeletionService(repo, new()).DeleteAsync(new(1, 2, "Lý do"), 1, RoleCodes.Admin, null)).Error);
    }
}
