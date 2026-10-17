using Dental.Application.Common;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Application.Features.MOD_RX.Services;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_RX;
public sealed class PrescriptionLookupLinkTests
{
    private static PrescriptionResponse Rx() => new(1, 7, 3, null, "RX-20261017-0001", "Synthetic", null, null, false, null, DateTime.UtcNow, null, 1, []);
    private static PrescriptionContext Context(PrescriptionResponse? rx) => new(7, 9, 3, VisitStatuses.InProgress, false, "", true, null, null, null, null, [], rx);
    [Fact]
    public async Task LinkContainsOnlySavedCodeVersionAndRelativeRoute()
    {
        var repo = Substitute.For<IPrescriptionRepository>();
        repo.GetContextAsync(7, 3, RoleCodes.Dentist, default).Returns(Result<PrescriptionContext>.Success(Context(Rx())));
        var result = await new PrescriptionService(repo).LookupLinkAsync(7, 3, RoleCodes.Dentist);
        Assert.True(result.IsSuccess); Assert.Equal("/portal/prescriptions/RX-20261017-0001", result.Value!.RelativePath);
        Assert.Equal(1, result.Value.Version);
        var json = System.Text.Json.JsonSerializer.Serialize(result.Value);
        Assert.DoesNotContain("Synthetic", json); Assert.DoesNotContain("Citizen", json); Assert.DoesNotContain("Token", json);
    }
    [Fact]
    public async Task NoOrderCancelledOrMalformedCodeNeverIssuesLink()
    {
        foreach (var rx in new[] { null, Rx() with { IsCancelled = true }, Rx() with { PrescriptionCode = "../patients" } })
        {
            var repo = Substitute.For<IPrescriptionRepository>();
            repo.GetContextAsync(7, 3, RoleCodes.Dentist, default).Returns(Result<PrescriptionContext>.Success(Context(rx)));
            Assert.True((await new PrescriptionService(repo).LookupLinkAsync(7, 3, RoleCodes.Dentist)).IsFailure);
        }
    }
    [Fact]
    public async Task ForeignOwnershipFailurePropagates()
    {
        var repo = Substitute.For<IPrescriptionRepository>();
        repo.GetContextAsync(7, 3, RoleCodes.Dentist, default).Returns(Result<PrescriptionContext>.Failure(Error.NotFound));
        Assert.Equal(Error.NotFound, (await new PrescriptionService(repo).LookupLinkAsync(7, 3, RoleCodes.Dentist)).Error);
    }
    [Fact]
    public async Task InvalidRoleNeverReadsRepository()
    {
        var repo = Substitute.For<IPrescriptionRepository>();
        Assert.True((await new PrescriptionService(repo).LookupLinkAsync(7, 3, "UNKNOWN")).IsFailure);
        await repo.DidNotReceiveWithAnyArgs().GetContextAsync(default, default, default!, default);
    }
}
