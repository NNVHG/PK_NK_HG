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
    [Fact] public void StaffProofCannotBeUsedForPatientChange()
    {
        var c=new Clock();using var patient=new PatientPhoneChangeProofs(c);using var staff=new StaffPhoneChangeProofs(c);
        var d=staff.Issue(new(7,4,"0967000001","actor","user")).Value!;
        Assert.Null(patient.Consume(4,new(d.Pending.ChallengeId,d.Code,Stamp,true)));
    }

    [Theory][InlineData("")][InlineData("abc\0def")]
    public void ChangeRejectsInvalidCurrentPassword(string password) => Assert.False(
        new PatientPhoneChangeValidator().Validate(new PatientPhoneChangeCommand("0967000001",Stamp,true,password)).IsValid);
    [Fact] public void CurrentPasswordUsesBcryptByteBoundaryAndAllowsLegacyStrength()
    {
        var validator=new PatientPhoneChangeValidator();
        Assert.True(validator.Validate(new PatientPhoneChangeCommand("0967000001",Stamp,true,"Aa1234")).IsValid);
        Assert.False(validator.Validate(new PatientPhoneChangeCommand("0967000001",Stamp,true,new string('Đ',37))).IsValid);
        Assert.False(validator.Validate(new PatientPhoneChangeCommand("0967000001",Stamp,false,"Aa1234")).IsValid);
    }
    [Fact] public async Task InvalidInputDoesNotReachRepositoryOrDelivery()
    {
        var repo=Substitute.For<IPatientPhoneChangeRepository>();var delivery=Substitute.For<IPatientActivationDelivery>();
        var service=new PatientPhoneChangeService(repo,delivery);
        Assert.True((await service.ChangeAsync(4,new("0967000001",Stamp,false,"Aa1234"),null,default)).IsFailure);
        Assert.True((await service.PreviewAsync(4,new("bad"),default)).IsFailure);
        Assert.Empty(repo.ReceivedCalls());Assert.Empty(delivery.ReceivedCalls());
    }
    [Fact] public async Task UnconfiguredActivationProviderPreventsMutation()
    {
        var repo=Substitute.For<IPatientPhoneChangeRepository>();var delivery=Substitute.For<IPatientActivationDelivery>();
        var service=new PatientPhoneChangeService(repo,delivery);
        Assert.Equal(PatientPhoneChangeService.Unavailable,(await service.ChangeAsync(4,new("0967000001",Stamp,true,"Aa1234"),null,default)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task ChangeCallsAtomicRepositoryWithoutSendingOtp()
    {
        var repo=Substitute.For<IPatientPhoneChangeRepository>();var delivery=Substitute.For<IPatientActivationDelivery>();
        delivery.IsConfigured.Returns(true);var request=new PatientPhoneChangeCommand("0967000001",Stamp,true,"Aa1234");
        repo.ChangeAsync(4,request,null,default).Returns(Result<PatientPhoneCompleted>.Success(new(true,true)));
        var result=await new PatientPhoneChangeService(repo,delivery).ChangeAsync(4,request,null,default);
        Assert.True(result.Value!.Completed);await repo.Received(1).ChangeAsync(4,request,null,default);
        delivery.DidNotReceive().Enqueue(Arg.Any<ActivationDispatch>());
    }
}
