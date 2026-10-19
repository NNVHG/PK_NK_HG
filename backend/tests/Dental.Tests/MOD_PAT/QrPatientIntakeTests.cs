using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Application.Features.Patients.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.MOD_PAT;
public sealed class QrPatientIntakeTests
{
    private static readonly VietnamClock Clock = new(TimeProvider.System);
    private static QrPatientIntakeRequest Valid() => new(new string('A',64),"123******789",new("Synthetic",new(1990,1,1),"Male","0990000111",Address:"Synthetic address"),true);
    [Fact] public void ValidHashAndIntakeAreAccepted()
    { Assert.True(new QrIdentityLookupValidator().Validate(new QrIdentityLookupRequest(new string('a',64))).IsValid);Assert.True(new QrPatientIntakeValidator(Clock).Validate(Valid()).IsValid); }
    [Theory] [InlineData("")] [InlineData("123456789012")] [InlineData("GGGG")] [InlineData(null)]
    public void LookupRejectsRawOrMalformedIdentity(string? value) => Assert.False(new QrIdentityLookupValidator().Validate(new QrIdentityLookupRequest(value!)).IsValid);
    [Fact] public void TrailingNewlineAndNonAsciiPhoneCannotPassRegexBoundaries()
    {
        Assert.False(new QrIdentityLookupValidator().Validate(new QrIdentityLookupRequest(new string('A',64)+"\n")).IsValid);
        var r=Valid();var v=new QrPatientIntakeValidator(Clock);
        foreach(var invalid in new[]{r with {CitizenIdMasked=r.CitizenIdMasked+"\n"},r with {CitizenIdHash=r.CitizenIdHash+"\n"},r with {Patient=r.Patient with {Phone="0990000111\n"}},r with {Patient=r.Patient with {Phone="0１２３４５６７８９"}}})Assert.False(v.Validate(invalid).IsValid);
    }
    [Fact] public void ConfirmationMaskAndDemographicsAreValidated()
    {
        var v=new QrPatientIntakeValidator(Clock);var r=Valid();
        foreach(var invalid in new[] {r with {Confirmed=false},r with {CitizenIdMasked="123456789012"},r with {CitizenIdMasked="***789"},r with {CitizenIdHash=new string('Z',64)},r with {Patient=null!},r with {Patient=r.Patient with {Phone="123"}},r with {Patient=r.Patient with {DateOfBirth=Clock.Today.AddDays(1)}},r with {Patient=r.Patient with {FullName=" "}},r with {Patient=r.Patient with {Address=new string('x',501)}}})
            Assert.False(v.Validate(invalid).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Dentist)] [InlineData(RoleCodes.Patient)] [InlineData(null)]
    public async Task DisallowedRolesNeverReachRepository(string? role)
    {
        var repo=Substitute.For<IQrPatientIntakeRepository>();var service=new QrPatientIntakeService(repo,Clock);
        Assert.Equal(Error.Forbidden,(await service.LookupAsync(1,role,new(new string('A',64)))).Error);
        Assert.Equal(Error.Forbidden,(await service.CreateAsync(1,role,Valid(),null)).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Theory] [InlineData(RoleCodes.Admin)] [InlineData(RoleCodes.Receptionist)]
    public async Task AllowedRolesDelegateExactBody(string role)
    {
        var repo=Substitute.For<IQrPatientIntakeRepository>();var service=new QrPatientIntakeService(repo,Clock);var r=Valid();
        await service.CreateAsync(2,role,r,"synthetic");await repo.Received().CreateAsync(2,role,r,"synthetic",default);
        var lookup=new QrIdentityLookupRequest(r.CitizenIdHash);await service.LookupAsync(2,role,lookup);await repo.Received().LookupAsync(2,role,lookup,default);
    }
    [Fact] public async Task InvalidBodyAndActorNeverReachRepository()
    {
        var repo=Substitute.For<IQrPatientIntakeRepository>();var service=new QrPatientIntakeService(repo,Clock);
        Assert.Equal(Error.Forbidden,(await service.CreateAsync(0,RoleCodes.Admin,Valid(),null)).Error);
        Assert.Equal(Error.Validation,(await service.CreateAsync(1,RoleCodes.Admin,Valid() with {Confirmed=false},null)).Error);
        Assert.Equal(Error.Validation,(await service.LookupAsync(1,RoleCodes.Admin,new("raw"))).Error);Assert.Empty(repo.ReceivedCalls());
    }
}
