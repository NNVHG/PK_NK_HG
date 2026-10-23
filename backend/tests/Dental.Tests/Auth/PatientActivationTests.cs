using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Dental.Application.Interfaces;
using Dental.Infrastructure.Services;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class PatientActivationTests
{
    private sealed class Clock : TimeProvider { public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now; }
    private static readonly ActivationTarget Target=new(7,new string('A',64));
    [Fact] public void RandomCodeAndCompletionTokenAreSeparateSingleUseProofs()
    {
        var c=new Clock();using var p=new PatientActivationProofs(c);var issued=p.Issue("0967000000",Target).Value!;
        Assert.Matches("^[0-9]{6}$",issued.Code);Assert.NotEqual(Guid.Empty,issued.ChallengeId);
        Assert.Equal(Target,p.ConsumeCode(new(issued.ChallengeId,issued.Code)));Assert.Null(p.ConsumeCode(new(issued.ChallengeId,issued.Code)));
        var grant=p.Grant(Target).Value!;Assert.Matches("^[A-F0-9]{64}$",grant.CompletionToken);
        Assert.Null(p.ConsumeGrant(issued.Code));Assert.Equal(Target,p.ConsumeGrant(grant.CompletionToken));Assert.Null(p.ConsumeGrant(grant.CompletionToken));
    }
    [Fact] public void FiveWrongCodesLockEvenTheCorrectCode()
    {
        using var p=new PatientActivationProofs(new Clock());var issued=p.Issue("0967000000",Target).Value!;
        var wrong=issued.Code=="000000"?"111111":"000000";for(var n=0;n<5;n++)Assert.Null(p.ConsumeCode(new(issued.ChallengeId,wrong)));
        Assert.Null(p.ConsumeCode(new(issued.ChallengeId,issued.Code)));
    }
    [Fact] public void OtpAndCompletionGrantExpireAtFiveMinutes()
    {
        var c=new Clock();using var p=new PatientActivationProofs(c);var issued=p.Issue("0967000000",Target).Value!;var grant=p.Grant(Target).Value!;
        c.Now+=TimeSpan.FromMinutes(5);Assert.Null(p.ConsumeCode(new(issued.ChallengeId,issued.Code)));Assert.Null(p.ConsumeGrant(grant.CompletionToken));
    }
    [Fact] public void ResendWaitsSixtySecondsAndInvalidatesTheOldCode()
    {
        var c=new Clock();using var p=new PatientActivationProofs(c);var a=p.Issue("0967000000",Target).Value!;
        Assert.Equal("AUTH_ACTIVATION_RATE",p.Issue("0967000000",null).Error.Code);c.Now+=TimeSpan.FromSeconds(60);
        var b=p.Issue("0967000000",Target).Value!;Assert.Null(p.ConsumeCode(new(a.ChallengeId,a.Code)));Assert.NotEqual(a.ChallengeId,b.ChallengeId);
        p.Revoke("0967000000",a.ChallengeId);Assert.True(p.IsCurrent("0967000000",b.ChallengeId));p.Revoke("0967000000",b.ChallengeId);Assert.Null(p.ConsumeCode(new(b.ChallengeId,b.Code)));
    }
    [Fact] public void UnknownOrIneligibleAccountCanNeverObtainTargetEvenWithItsGeneratedCode()
    {
        using var p=new PatientActivationProofs(new Clock());var issued=p.Issue("0967000000",null).Value!;
        Assert.Null(p.ConsumeCode(new(issued.ChallengeId,issued.Code)));
    }
    [Fact] public async Task ConcurrentConsumptionHasOneWinner()
    {
        using var p=new PatientActivationProofs(new Clock());var issued=p.Issue("0967000000",Target).Value!;
        var results=await Task.WhenAll(Enumerable.Range(0,20).Select(_=>Task.Run(()=>p.ConsumeCode(new(issued.ChallengeId,issued.Code)))));
        Assert.Single(results, x=>x is not null);
    }
    [Theory][InlineData("Abc1234")][InlineData("abcdefgh")][InlineData("ABCDEFGH")][InlineData("Abcdefgh")][InlineData("abc12345")]
    public void UserSelectedPasswordRequiresEightCharactersAndUpperLowerDigit(string password)
    {
        Assert.False(new ActivationCompletionValidator().Validate(new ActivationCompletion(new string('A',64),false,password,true)).IsValid);
    }
    [Fact] public void PasswordChoiceIsExplicitAndBcryptByteLimitIsRespected()
    {
        var v=new ActivationCompletionValidator();var token=new string('A',64);
        Assert.True(v.Validate(new ActivationCompletion(token,true,null,true)).IsValid);Assert.True(v.Validate(new ActivationCompletion(token,false,"Strong123",true)).IsValid);
        Assert.False(v.Validate(new ActivationCompletion(token,true,"Strong123",true)).IsValid);Assert.False(v.Validate(new ActivationCompletion(token,true,null,false)).IsValid);
        Assert.False(v.Validate(new ActivationCompletion(token,false,"Aa1"+new string('Đ',40),true)).IsValid);Assert.False(v.Validate(new ActivationCompletion(token,false,"Strong1\0x",true)).IsValid);
    }
    [Fact] public async Task WrongPasswordNeverDispatchesOrBindsARealTarget()
    {
        var repo=Substitute.For<IPatientActivationRepository>();var proofs=Substitute.For<IPatientActivationProofs>();var delivery=Substitute.For<IPatientActivationDelivery>();var passwords=Substitute.For<IPasswordHasher>();
        repo.FindPendingAsync("0967000000",default).Returns(new ActivationAccount(Target,"hash","0967000000"));passwords.Verify("Incorrect1","hash").Returns(false);delivery.IsConfigured.Returns(true);delivery.ShowDevelopmentCode.Returns(true);
        var dispatch=new ActivationDispatch("0967000000",Guid.NewGuid(),"123456");proofs.Issue(dispatch.Phone,null).Returns(Result<ActivationDispatch>.Success(dispatch));
        var svc=new PatientActivationService(repo,proofs,delivery,passwords);var result=await svc.RequestAsync(new(dispatch.Phone,"Incorrect1"));Assert.True(result.IsSuccess);Assert.Null(result.Value!.DevelopmentCode);
        delivery.DidNotReceive().Enqueue(Arg.Any<ActivationDispatch>());proofs.Received(1).Issue(dispatch.Phone,null);
    }
    [Fact] public async Task MissingSmsConfigurationFailsBeforeLookup()
    {
        var repo=Substitute.For<IPatientActivationRepository>();var svc=new PatientActivationService(repo,Substitute.For<IPatientActivationProofs>(),Substitute.For<IPatientActivationDelivery>(),Substitute.For<IPasswordHasher>());
        Assert.Equal(PatientActivationService.Unavailable,(await svc.RequestAsync(new("0967000000","0967000000"))).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task ChangedAccountCannotReceiveCompletionGrant()
    {
        var repo=Substitute.For<IPatientActivationRepository>();var proofs=Substitute.For<IPatientActivationProofs>();var req=new ActivationVerification(Guid.NewGuid(),"123456");proofs.ConsumeCode(req).Returns(Target);
        var svc=new PatientActivationService(repo,proofs,Substitute.For<IPatientActivationDelivery>(),Substitute.For<IPasswordHasher>());
        Assert.Equal(PatientActivationService.InvalidProof,(await svc.VerifyAsync(req)).Error);proofs.DidNotReceive().Grant(Arg.Any<ActivationTarget>());
    }
    [Fact] public async Task InvalidPasswordDoesNotConsumeGrantAndSkipNeverChangesPassword()
    {
        var repo=Substitute.For<IPatientActivationRepository>();var proofs=Substitute.For<IPatientActivationProofs>();var passwords=Substitute.For<IPasswordHasher>();
        var svc=new PatientActivationService(repo,proofs,Substitute.For<IPatientActivationDelivery>(),passwords);var token=new string('A',64);
        Assert.Equal(Error.Validation,(await svc.CompleteAsync(new(token,false,"Short1",true),null)).Error);Assert.Empty(proofs.ReceivedCalls());
        proofs.ConsumeGrant(token).Returns(Target);repo.CompleteAsync(Target,null,null,default).Returns(true);
        Assert.True((await svc.CompleteAsync(new(token,true,null,true),null)).IsSuccess);Assert.Empty(passwords.ReceivedCalls());
    }
    [Fact] public void PublicDtosContainNoAccountIdentityOrCredentials()
    {
        Assert.Equal(new[]{"ChallengeId","DevelopmentCode","ExpiresInSeconds","ResendAfterSeconds"},typeof(ActivationChallenge).GetProperties().Select(p=>p.Name).Order().ToArray());
        Assert.Equal(new[]{"CompletionToken","ExpiresInSeconds","MustChangePassword"},typeof(ActivationGrant).GetProperties().Select(p=>p.Name).Order().ToArray());
    }

    [Theory][InlineData(false)][InlineData(true)]
    public async Task CorrectPasswordReturnsSupportCodeOnlyWhenDeliveryAllowsDevelopment(bool development)
    {
        var repo=Substitute.For<IPatientActivationRepository>();var proofs=Substitute.For<IPatientActivationProofs>();
        var delivery=Substitute.For<IPatientActivationDelivery>();var passwords=Substitute.For<IPasswordHasher>();
        const string phone="0967000000";const string password="Synthetic123";
        repo.FindPendingAsync(phone,default).Returns(new ActivationAccount(Target,"hash",phone));
        passwords.Verify(password,"hash").Returns(true);delivery.IsConfigured.Returns(true);delivery.ShowDevelopmentCode.Returns(development);
        var dispatch=new ActivationDispatch(phone,Guid.NewGuid(),"123456");proofs.Issue(phone,Target).Returns(Result<ActivationDispatch>.Success(dispatch));delivery.Enqueue(dispatch).Returns(true);
        var result=await new PatientActivationService(repo,proofs,delivery,passwords).RequestAsync(new(phone,password));
        Assert.True(result.IsSuccess);Assert.Equal(development?dispatch.Code:null,result.Value!.DevelopmentCode);
        delivery.Received(1).Enqueue(dispatch);
    }
}
