using System.Reflection;
using System.Text.Json;
using Dental.Api.Controllers;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.MOD_RX;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using NSubstitute;
using Xunit;

namespace Dental.Tests.MOD_RX;
public sealed class PublicPrescriptionTests
{
    private static readonly KeyedIdentityHasher IdentityHasher = new(new byte[32]);
    private const string Code = "RX-20261017-0001";
    [Theory] [InlineData("")] [InlineData("RX-1")] [InlineData("../patients")] [InlineData("rx-20261017-0001")] [InlineData("RX-20261017-0001\n")] [InlineData(null)]
    public void InvalidCodeRejected(string? code) => Assert.False(PublicPrescriptionService.ValidCode(code));
    [Fact]
    public void PublicDtoNeverExposesIdentityMedicalOrMoneyBeforeChallenge()
    {
        var json = JsonSerializer.Serialize(new PublicPrescriptionSummary(Code, DateTime.UtcNow));
        Assert.DoesNotContain("Patient", json); Assert.DoesNotContain("Citizen", json); Assert.DoesNotContain("Diagnosis", json);
        var controller = typeof(PublicPrescriptionsController);
        Assert.NotNull(controller.GetCustomAttribute<AllowAnonymousAttribute>());
        Assert.NotNull(controller.GetCustomAttribute<EnableRateLimitingAttribute>());
        Assert.True(controller.GetCustomAttribute<ResponseCacheAttribute>()!.NoStore);
    }
    [Fact]
    public void TokenBoundToCodeIdentityVersionAndExpiry()
    {
        var time = new Clock(); using var access = new PublicPrescriptionAccess(new EphemeralDataProtectionProvider(), time);
        var session = access.Issue(Code, 3, 2);
        Assert.True(access.Validate(Code, 3, 2, session.Token));
        Assert.False(access.Validate("RX-20261017-0002", 3, 2, session.Token));
        Assert.False(access.Validate(Code, 4, 2, session.Token));
        Assert.False(access.Validate(Code, 3, 3, session.Token));
        Assert.False(access.Validate(Code, 3, 2, session.Token + "x"));
        Assert.False(access.Validate(Code, 3, 2, "AccountJwtIsNotViewToken"));
        time.Now += TimeSpan.FromMinutes(15);
        Assert.False(access.Validate(Code, 3, 2, session.Token));
    }
    [Fact]
    public void FiveWrongAttemptsLockForFifteenMinutesAndCorrectInputCannotBypassLock()
    {
        var time = new Clock(); using var access = new PublicPrescriptionAccess(new EphemeralDataProtectionProvider(), time);
        for (var i = 0; i < 4; i++) Assert.True(access.RecordResult(Code, false));
        Assert.False(access.RecordResult(Code, false)); Assert.True(access.IsLocked(Code));
        Assert.False(access.RecordResult(Code, true));
        time.Now += TimeSpan.FromMinutes(15);
        Assert.False(access.IsLocked(Code)); Assert.True(access.RecordResult(Code, true));
    }
    [Fact]
    public async Task ConcurrentWrongAttemptsCannotLoseCounts()
    {
        using var access = new PublicPrescriptionAccess(new EphemeralDataProtectionProvider(), new Clock());
        var results = await Task.WhenAll(Enumerable.Range(0, 30).Select(_ => Task.Run(() => access.RecordResult(Code, false))));
        Assert.Equal(4, results.Count(x => x)); Assert.True(access.IsLocked(Code));
    }
    [Fact]
    public async Task WrongIdentityDoesNotIssueTokenAndMissingTokenDoesNotReadDetails()
    {
        var repo = Substitute.For<IPublicPrescriptionRepository>(); var access = Substitute.For<IPublicPrescriptionAccess>();
        repo.ProofAsync(Code, default).Returns(new PublicPrescriptionProof(3, 2, CitizenIdIdentity.Hash("987654321098")));
        access.RecordResult(Code, false).Returns(true);
        var service = new PublicPrescriptionService(repo, access, IdentityHasher);
        Assert.True((await service.VerifyAsync(Code, new("987654321099"))).IsFailure);
        Assert.True((await service.DetailsAsync(Code, null)).IsFailure);
        access.DidNotReceiveWithAnyArgs().Issue(default!, default, default);
        await repo.DidNotReceiveWithAnyArgs().DetailsAsync(default!, default, default);
    }
    [Fact]
    public async Task MissingIdentityHasNoFallbackAndUnknownCodeDoesNotFillAttemptCache()
    {
        var repo = Substitute.For<IPublicPrescriptionRepository>(); var access = Substitute.For<IPublicPrescriptionAccess>();
        access.RecordResult(Code, false).Returns(true);
        var service = new PublicPrescriptionService(repo, access, IdentityHasher);
        Assert.True((await service.VerifyAsync(Code, new("987654321098"))).IsFailure);
        access.DidNotReceiveWithAnyArgs().RecordResult(default!, default);
        repo.ProofAsync(Code, default).Returns(new PublicPrescriptionProof(3, 0, null));
        Assert.True((await service.VerifyAsync(Code, new("987654321098"))).IsFailure);
        access.DidNotReceiveWithAnyArgs().Issue(default!, default, default);
    }
    [Fact]
    public async Task MatchingIdentityIssuesOnlyRestrictedSession()
    {
        var repo = Substitute.For<IPublicPrescriptionRepository>(); var access = Substitute.For<IPublicPrescriptionAccess>();
        repo.ProofAsync(Code, default).Returns(new PublicPrescriptionProof(3, 2, CitizenIdIdentity.Hash("987654321098")));
        access.RecordResult(Code, true).Returns(true); access.Issue(Code, 3, 2).Returns(new PublicPrescriptionSession("view-token", DateTimeOffset.UtcNow.AddMinutes(15)));
        var result = await new PublicPrescriptionService(repo, access, IdentityHasher).VerifyAsync(Code, new("987654321098"));
        Assert.True(result.IsSuccess); Assert.Equal("view-token", result.Value!.Token);
    }
    private sealed class Clock : TimeProvider
    {
        public DateTimeOffset Now = new(2026, 10, 17, 0, 0, 0, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => Now;
    }
}
