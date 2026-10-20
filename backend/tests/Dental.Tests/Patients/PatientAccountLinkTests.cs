using Dental.Application.Common;
using Dental.Application.Features.MOD_PAT;
using Dental.Domain.Constants;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Patients;
public sealed class PatientAccountLinkTests
{
    private static LinkPatientAccountRequest Valid => new(7,new string('A',64),true);
    [Theory][InlineData(RoleCodes.Dentist)][InlineData(RoleCodes.Assistant)][InlineData(RoleCodes.Patient)][InlineData(null)][InlineData("UNKNOWN")]
    public async Task OtherRolesCannotReadOrLink(string? role)
    {
        var repo=Substitute.For<IPatientAccountLinkRepository>();var service=new PatientAccountLinkService(repo);
        Assert.Equal(Error.Forbidden,(await service.ReadAsync(3,1,role)).Error);
        Assert.Equal(Error.Forbidden,(await service.LinkAsync(3,1,role,Valid,null)).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Theory][InlineData(RoleCodes.Admin)][InlineData(RoleCodes.Receptionist)]
    public async Task AllowedRolesForwardExactRequestAndActor(string role)
    {
        var repo=Substitute.For<IPatientAccountLinkRepository>();var service=new PatientAccountLinkService(repo);var request=Valid;
        await service.ReadAsync(3,1,role);await repo.Received().ReadAsync(3,1,role,default);
        await service.LinkAsync(3,1,role,request,"test-ip");await repo.Received().LinkAsync(3,1,role,request,"test-ip",default);
    }
    [Fact]public async Task InvalidIdsAndActorNeverReachRepository()
    {
        var repo=Substitute.For<IPatientAccountLinkRepository>();var service=new PatientAccountLinkService(repo);
        Assert.Equal(Error.Validation,(await service.ReadAsync(0,1,RoleCodes.Admin)).Error);
        Assert.Equal(Error.Validation,(await service.LinkAsync(-1,1,RoleCodes.Admin,Valid,null)).Error);
        Assert.Equal(Error.Forbidden,(await service.LinkAsync(3,0,RoleCodes.Admin,Valid,null)).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Theory][InlineData(0,true,"A")][InlineData(7,false,"A")][InlineData(7,true,"")][InlineData(7,true,"Z")][InlineData(7,true,"a")]
    public async Task InvalidTargetConfirmationOrStampRejected(int target,bool confirmed,string digit)
    {
        var request=new LinkPatientAccountRequest(target,digit.Length==0?"":new string(digit[0],64),confirmed);
        var repo=Substitute.For<IPatientAccountLinkRepository>();var service=new PatientAccountLinkService(repo);
        Assert.Equal(Error.Validation,(await service.LinkAsync(3,1,RoleCodes.Admin,request,null)).Error);Assert.Empty(repo.ReceivedCalls());
    }
    [Fact]public void ReviewStampHasExactBoundedFormat()
    {
        var validator=new LinkPatientAccountValidator();Assert.True(validator.Validate(Valid).IsValid);
        Assert.False(validator.Validate(Valid with{ReviewStamp=new string('A',63)}).IsValid);
        Assert.False(validator.Validate(Valid with{ReviewStamp=new string('A',65)}).IsValid);
        Assert.False(validator.Validate(Valid with{ReviewStamp=null!}).IsValid);
    }
    [Fact]public void CandidateNeverReturnsCredentialsOrOtherFamilyProfiles()
        => Assert.Equal(new[]{"DateOfBirth","FullName","Phone","UserId"},typeof(FamilyAccountCandidate).GetProperties().Select(p=>p.Name).Order().ToArray());
}
