using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;
public sealed class PatientPhoneChangeTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now = DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow() => Now; }
    private static readonly string Stamp = new('A', 64);
    private static PatientPhoneTarget Target(int id = 4) => new(id, "0967000001", Stamp);
    [Theory][InlineData("")][InlineData("bad")][InlineData("0967000001\n")][InlineData("0１２３４５６７８９")][InlineData("1967000001")][InlineData("096700001")]
    public void PreviewRejectsMalformedPhone(string phone) => Assert.False(new PatientPhonePreviewValidator().Validate(new PatientPhonePreviewRequest(phone)).IsValid);
    [Theory][InlineData("")][InlineData("AA")][InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public void RequestRequiresRealReviewStamp(string stamp) => Assert.False(new PatientPhoneRequestValidator().Validate(new PatientPhoneRequest("0967000001", stamp, true)).IsValid);
    [Fact] public void BothWritesRequireExplicitConsent()
    {
        Assert.False(new PatientPhoneRequestValidator().Validate(new PatientPhoneRequest("0967000001", Stamp, false)).IsValid);
        Assert.False(new PatientPhoneConfirmationValidator().Validate(new PatientPhoneConfirmation(Guid.NewGuid(), "123456", Stamp, false)).IsValid);
    }
    [Theory][InlineData("12345")][InlineData("123456\n")][InlineData("１２３４５６")]
    public void ConfirmRejectsMalformedOtp(string code) => Assert.False(new PatientPhoneConfirmationValidator().Validate(new PatientPhoneConfirmation(Guid.NewGuid(), code, Stamp, true)).IsValid);
    [Fact] public void ProofBindsOwnerReviewAndChallenge()
    {
        using var p = new PatientPhoneChangeProofs(new Clock()); var t = Target(); var d = p.Issue(t).Value!;
        Assert.Matches("^[0-9]{6}$", d.Code);
        Assert.Null(p.Consume(8, new(d.Pending.ChallengeId, d.Code, Stamp, true)));
        Assert.Null(p.Consume(4, new(d.Pending.ChallengeId, d.Code, new('B',64), true)));
        Assert.Null(p.Consume(4, new(Guid.NewGuid(), d.Code, Stamp, true)));
        Assert.Equal(t, p.Consume(4, new(d.Pending.ChallengeId, d.Code, Stamp, true)));
        Assert.Null(p.Consume(4, new(d.Pending.ChallengeId, d.Code, Stamp, true)));
    }
    [Fact] public void FifthWrongCodeLocksAndCannotBypassCooldown()
    {
        using var p = new PatientPhoneChangeProofs(new Clock()); var d = p.Issue(Target()).Value!;
        for (var n=0;n<5;n++) Assert.Null(p.Consume(4,new(d.Pending.ChallengeId,d.Code=="000000"?"111111":"000000",Stamp,true)));
        Assert.Null(p.Consume(4,new(d.Pending.ChallengeId,d.Code,Stamp,true))); Assert.True(p.Issue(Target()).IsFailure);
    }
    [Fact] public void ExpiryAndResendAreStrict()
    {
        var c=new Clock();using var p=new PatientPhoneChangeProofs(c);var a=p.Issue(Target()).Value!;
        c.Now+=TimeSpan.FromSeconds(59);Assert.True(p.Issue(Target()).IsFailure);
        c.Now+=TimeSpan.FromSeconds(1);var b=p.Issue(Target()).Value!;p.Revoke(4,a.Pending.ChallengeId);
        Assert.Null(p.Consume(4,new(a.Pending.ChallengeId,a.Code,Stamp,true)));
        c.Now+=TimeSpan.FromMinutes(5);Assert.Null(p.Consume(4,new(b.Pending.ChallengeId,b.Code,Stamp,true)));
    }
    [Fact] public async Task ConcurrentConsumptionHasOneWinner()
    {
        using var p=new PatientPhoneChangeProofs(new Clock());var d=p.Issue(Target()).Value!;
        var results=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Task.Run(()=>p.Consume(4,new(d.Pending.ChallengeId,d.Code,Stamp,true)))));
        Assert.Single(results,x=>x is not null);
    }
    [Fact] public void StaffProofCannotBeUsedForFamilyChange()
    {
        var c=new Clock();using var family=new PatientPhoneChangeProofs(c);using var staff=new StaffPhoneChangeProofs(c);
        var d=staff.Issue(new(7,4,"0967000001","actor","user")).Value!;
        Assert.Null(family.Consume(4,new(d.Pending.ChallengeId,d.Code,Stamp,true)));
    }
    private static (PatientPhoneChangeService Service,IPatientPhoneChangeRepository Repo,IPatientPhoneChangeSms Sms,PatientPhoneChangeProofs Proofs) Setup()
    {
        var repo=Substitute.For<IPatientPhoneChangeRepository>();var sms=Substitute.For<IPatientPhoneChangeSms>();var proofs=new PatientPhoneChangeProofs(new Clock());
        repo.PrepareAsync(4,Arg.Any<PatientPhoneRequest>(),null,Arg.Any<CancellationToken>()).Returns(Result<PatientPhoneTarget>.Success(Target()));
        sms.IsConfigured.Returns(true);sms.SendAsync(Arg.Any<string>(),Arg.Any<string>(),Arg.Any<CancellationToken>()).Returns(true);
        return(new(repo,proofs,sms),repo,sms,proofs);
    }
    [Fact] public async Task InvalidInputsDoNotReachDataOrConsumeProof()
    {
        var f=Setup();using var p=f.Proofs;
        Assert.True((await f.Service.PreviewAsync(4,new("bad"),default)).IsFailure);
        Assert.True((await f.Service.RequestAsync(4,new("0967000001",Stamp,false),null,default)).IsFailure);
        Assert.True((await f.Service.ConfirmAsync(4,new(Guid.Empty,"123456",Stamp,true),null,default)).IsFailure);
        Assert.Empty(f.Repo.ReceivedCalls());Assert.Empty(f.Sms.ReceivedCalls());
    }
    [Fact] public async Task StaleScopeDoesNotDispatchSms()
    {
        var f=Setup();using var p=f.Proofs;f.Repo.PrepareAsync(4,Arg.Any<PatientPhoneRequest>(),null,default).Returns(Result<PatientPhoneTarget>.Failure(PatientPhoneChangeService.Stale));
        Assert.Equal(PatientPhoneChangeService.Stale,(await f.Service.RequestAsync(4,new("0967000001",Stamp,true),null,default)).Error);
        await f.Sms.DidNotReceive().SendAsync(Arg.Any<string>(),Arg.Any<string>(),default);
    }
    [Theory][InlineData(false)][InlineData(true)] public async Task FailedDeliveryRevokesProof(bool throws)
    {
        var repo=Substitute.For<IPatientPhoneChangeRepository>();var proofs=Substitute.For<IPatientPhoneChangeProofs>();var sms=Substitute.For<IPatientPhoneChangeSms>();
        var d=new PatientPhoneDispatch(new(Guid.NewGuid(),"0967000001",DateTimeOffset.UtcNow.AddMinutes(5)),"123456");
        repo.PrepareAsync(4,Arg.Any<PatientPhoneRequest>(),null,default).Returns(Result<PatientPhoneTarget>.Success(Target()));
        proofs.Issue(Target()).Returns(Result<PatientPhoneDispatch>.Success(d));sms.IsConfigured.Returns(true);
        sms.SendAsync(Arg.Any<string>(),Arg.Any<string>(),default).Returns(_=>throws?throw new HttpRequestException("Synthetic failure"):Task.FromResult(false));
        var service=new PatientPhoneChangeService(repo,proofs,sms);
        Assert.Equal(PatientPhoneChangeService.Unavailable,(await service.RequestAsync(4,new("0967000001",Stamp,true),null,default)).Error);
        proofs.Received(1).Revoke(4,d.Pending.ChallengeId);await repo.DidNotReceive().CompleteAsync(Arg.Any<PatientPhoneTarget>(),null,default);
    }
    [Fact] public async Task UnconfiguredProviderDoesNotIssueOrSend()
    {
        var f=Setup();using var p=f.Proofs;f.Sms.IsConfigured.Returns(false);
        Assert.Equal(PatientPhoneChangeService.Unavailable,(await f.Service.RequestAsync(4,new("0967000001",Stamp,true),null,default)).Error);
        await f.Sms.DidNotReceive().SendAsync(Arg.Any<string>(),Arg.Any<string>(),default);
        Assert.True(p.Issue(Target()).IsSuccess);
    }
    [Fact] public void ExplicitRevokeInvalidatesIssuedCode()
    {
        using var p=new PatientPhoneChangeProofs(new Clock());var d=p.Issue(Target()).Value!;p.Revoke(4,d.Pending.ChallengeId);
        Assert.Null(p.Consume(4,new(d.Pending.ChallengeId,d.Code,Stamp,true)));
    }
    [Fact] public async Task SuccessOnlyCallsAtomicRepositoryOnce()
    {
        var f=Setup();using var p=f.Proofs;var d=p.Issue(Target()).Value!;f.Repo.CompleteAsync(Target(),null,default).Returns(Result<PatientPhoneCompleted>.Success(new(true,2)));
        var request=new PatientPhoneConfirmation(d.Pending.ChallengeId,d.Code,Stamp,true);
        Assert.Equal(2,(await f.Service.ConfirmAsync(4,request,null,default)).Value!.UpdatedProfiles);
        Assert.True((await f.Service.ConfirmAsync(4,request,null,default)).IsFailure);await f.Repo.Received(1).CompleteAsync(Target(),null,default);
    }
}
