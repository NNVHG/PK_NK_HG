using Dental.Application.Common;
using Dental.Application.Features.Staff;
using Dental.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Staff;
public sealed class StaffPhoneChangeTests
{
    private sealed class Clock : TimeProvider
    { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private static StaffPhoneChangeTarget Target(int id = 4, string phone = "0967000001") => new(1, id, phone, "actor-stamp", "user-stamp");
    [Theory]
    [InlineData("")][InlineData("1967000001")][InlineData("096700001")][InlineData("09670000011")]
    [InlineData("0967000001\n")][InlineData("0１２３４５６７８９")][InlineData(" 096700001")]
    public void RequestRejectsMalformedPhone(string phone) => Assert.False(new StaffPhoneChangeRequestValidator().Validate(new StaffPhoneChangeRequest(phone, true)).IsValid);
    [Fact] public void RequestRequiresExplicitConfirmation() => Assert.False(new StaffPhoneChangeRequestValidator().Validate(new StaffPhoneChangeRequest("0967000001", false)).IsValid);
    [Theory][InlineData("")][InlineData("12345")][InlineData("1234567")][InlineData("123456\n")][InlineData("１２３４５６")]
    public void ConfirmRejectsMalformedCode(string code) => Assert.False(new StaffPhoneChangeConfirmationValidator().Validate(new StaffPhoneChangeConfirmation(Guid.NewGuid(), code, true)).IsValid);
    [Fact] public void ConfirmRequiresIdAndConsent()
    {
        var validator = new StaffPhoneChangeConfirmationValidator();
        Assert.False(validator.Validate(new StaffPhoneChangeConfirmation(Guid.Empty, "123456", true)).IsValid);
        Assert.False(validator.Validate(new StaffPhoneChangeConfirmation(Guid.NewGuid(), "123456", false)).IsValid);
    }
    [Fact] public void ProofBindsOwnerAndChallengeAndCanOnlyBeUsedOnce()
    {
        using var p = new StaffPhoneChangeProofs(new Clock()); var t = Target(); var d = p.Issue(t).Value!;
        Assert.Matches("^[0-9]{6}$", d.Code);
        Assert.Null(p.Pending(8)); Assert.Equal(t.NewPhone, p.Pending(4)!.NewPhone);
        Assert.Null(p.Consume(8, new(d.Pending.ChallengeId, d.Code, true)));
        Assert.Null(p.Consume(4, new(Guid.NewGuid(), d.Code, true)));
        Assert.Equal(t, p.Consume(4, new(d.Pending.ChallengeId, d.Code, true)));
        Assert.Null(p.Consume(4, new(d.Pending.ChallengeId, d.Code, true))); Assert.Null(p.Pending(4));
    }
    [Fact] public void FifthWrongAttemptLocksEvenTheCorrectCode()
    {
        using var p = new StaffPhoneChangeProofs(new Clock()); var d = p.Issue(Target()).Value!;
        var wrong = d.Code == "000000" ? "111111" : "000000";
        for (var i = 0; i < 5; i++) Assert.Null(p.Consume(4, new(d.Pending.ChallengeId, wrong, true)));
        Assert.Null(p.Consume(4, new(d.Pending.ChallengeId, d.Code, true)));
        Assert.True(p.Issue(Target()).IsFailure); // Wrong attempts cannot bypass resend cooldown.
    }
    [Fact] public void ProofExpiresExactlyAtFiveMinutes()
    {
        var clock = new Clock(); using var p = new StaffPhoneChangeProofs(clock); var d = p.Issue(Target()).Value!;
        clock.Now += TimeSpan.FromMinutes(5); Assert.Null(p.Pending(4)); Assert.Null(p.Consume(4, new(d.Pending.ChallengeId, d.Code, true)));
    }
    [Fact] public void ResendingReplacesOldProofAfterSixtySeconds()
    {
        var clock = new Clock(); using var p = new StaffPhoneChangeProofs(clock); var a = p.Issue(Target()).Value!;
        clock.Now += TimeSpan.FromSeconds(59); Assert.True(p.Issue(Target()).IsFailure);
        clock.Now += TimeSpan.FromSeconds(1); var b = p.Issue(Target(phone:"0967000002")).Value!;
        p.Revoke(4, a.Pending.ChallengeId); Assert.Equal(b.Pending, p.Pending(4));
        Assert.Null(p.Consume(4, new(a.Pending.ChallengeId, a.Code, true)));
        Assert.Equal("0967000002", p.Consume(4, new(b.Pending.ChallengeId, b.Code, true))!.NewPhone);
    }
    [Fact] public async Task ConcurrentConsumptionHasExactlyOneWinner()
    {
        using var p = new StaffPhoneChangeProofs(new Clock()); var d = p.Issue(Target()).Value!;
        var results = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => Task.Run(() => p.Consume(4, new(d.Pending.ChallengeId, d.Code, true)))));
        Assert.Single(results, x => x is not null);
    }
    private static (StaffPhoneChangeService Service, IStaffPhoneChangeRepository Repo, IStaffPhoneChangeSms Sms, StaffPhoneChangeProofs Proofs) Setup()
    {
        var repo = Substitute.For<IStaffPhoneChangeRepository>(); var sms = Substitute.For<IStaffPhoneChangeSms>(); var proofs = new StaffPhoneChangeProofs(new Clock());
        repo.PrepareAsync(1, 4, "0967000001", null, Arg.Any<CancellationToken>()).Returns(Result<StaffPhoneChangeTarget>.Success(Target()));
        repo.CanReadAsync(4, Arg.Any<CancellationToken>()).Returns(true); sms.IsConfigured.Returns(true);
        sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(true);
        return (new(repo, proofs, sms), repo, sms, proofs);
    }
    [Fact] public async Task InvalidInputNeverCallsRepositoryOrSms()
    {
        var f = Setup(); using var p = f.Proofs;
        Assert.True((await f.Service.RequestAsync(1, 4, new("bad", true), null, default)).IsFailure);
        Assert.True((await f.Service.ConfirmAsync(4, new(Guid.Empty, "123456", true), null, default)).IsFailure);
        Assert.Empty(f.Repo.ReceivedCalls()); Assert.Empty(f.Sms.ReceivedCalls());
    }
    [Fact] public async Task ForbiddenActorCannotIssueEvenWithUnavailableSms()
    {
        var f = Setup(); using var p = f.Proofs; f.Repo.PrepareAsync(1, 4, "0967000001", null, default).Returns(Result<StaffPhoneChangeTarget>.Failure(Error.Forbidden)); f.Sms.IsConfigured.Returns(false);
        var r = await f.Service.RequestAsync(1, 4, new("0967000001", true), null, default);
        Assert.Equal(Error.Forbidden, r.Error); Assert.Null(p.Pending(4));
    }
    [Fact] public async Task UnavailableSmsCreatesNoUsableProof()
    {
        var f = Setup(); using var p = f.Proofs; f.Sms.IsConfigured.Returns(false);
        Assert.Equal(StaffPhoneChangeService.Unavailable, (await f.Service.RequestAsync(1, 4, new("0967000001", true), null, default)).Error); Assert.Null(p.Pending(4));
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task DeliveryFailureRevokesChallenge(bool throws)
    {
        var f = Setup(); using var p = f.Proofs;
        f.Sms.SendAsync(Arg.Any<string>(), Arg.Any<string>(), default).Returns(_ => throws ? throw new HttpRequestException("Synthetic failure") : Task.FromResult(false));
        Assert.True((await f.Service.RequestAsync(1, 4, new("0967000001", true), null, default)).IsFailure); Assert.Null(p.Pending(4));
    }
    [Fact] public async Task SmsAndConfirmationAreBoundToRequestedNewPhoneAndOwner()
    {
        var f = Setup(); using var p = f.Proofs; string? code = null;
        f.Sms.SendAsync("0967000001", Arg.Any<string>(), default).Returns(c => { code = c.ArgAt<string>(1); return true; });
        var r = await f.Service.RequestAsync(1, 4, new("0967000001", true), null, default);
        Assert.True(r.IsSuccess); await f.Sms.Received(1).SendAsync("0967000001", Arg.Any<string>(), default);
        f.Repo.CompleteAsync(Target(), null, default).Returns(Result<bool>.Success(true));
        Assert.True((await f.Service.ConfirmAsync(4, new(r.Value!.ChallengeId, code!, true), null, default)).IsSuccess);
        Assert.True((await f.Service.ConfirmAsync(4, new(r.Value!.ChallengeId, code!, true), null, default)).IsFailure);
        await f.Repo.Received(1).CompleteAsync(Target(), null, default);
    }
    [Fact] public async Task ChangedAccountCannotConsumeOrReadProof()
    {
        var f = Setup(); using var p = f.Proofs; var d = p.Issue(Target()).Value!; f.Repo.CanReadAsync(4, default).Returns(false);
        Assert.Equal(Error.Forbidden, (await f.Service.PendingAsync(4, default)).Error);
        Assert.Equal(Error.Forbidden, (await f.Service.ConfirmAsync(4, new(d.Pending.ChallengeId, d.Code, true), null, default)).Error);
        Assert.NotNull(p.Pending(4));
    }
}
