using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Services;
using Dental.Application.Features.MOD_APP;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using NSubstitute;
using Xunit;

namespace Dental.Tests.Appointments;

public sealed class BookingApprovalTests
{
    private static ApproveBookingRequest Valid() => new(1, new string('A',64), null, true, true, true);
    [Fact] public void NewAndExistingProfilesRequireDistinctExplicitChoices()
    {
        var v = new ApproveBookingRequestValidator();
        Assert.True(v.Validate(Valid()).IsValid);
        Assert.True(v.Validate(Valid() with { PatientId = 8, ConfirmedNewPatient = false }).IsValid);
        Assert.False(v.Validate(Valid() with { PatientId = 8 }).IsValid);
        Assert.False(v.Validate(Valid() with { ConfirmedNewPatient = false }).IsValid);
    }
    [Theory]
    [InlineData(0, "stamp", true)] [InlineData(1,"stamp",true)] [InlineData(1,"",true)]
    [InlineData(int.MaxValue,"valid",true)] [InlineData(1,"valid",false)]
    public void InvalidProofOrVersionRejected(int version,string proof,bool confirm)
    {
        Assert.False(new ApproveBookingRequestValidator().Validate(Valid() with {
            ExpectedVersion=version, ReviewStamp=proof=="valid"?new string('A',64):proof, Confirmed=confirm }).IsValid);
    }
    [Theory] [InlineData(RoleCodes.Patient)] [InlineData(RoleCodes.Assistant)] [InlineData(RoleCodes.Dentist)] [InlineData(null)]
    public async Task ReviewAndApproveRemainStaffOnly(string? role)
    {
        var repo=Substitute.For<IBookingApprovalRepository>();var service=new BookingApprovalService(repo);
        Assert.Equal(Error.Forbidden,(await service.ReadAsync(1,7,role)).Error);
        Assert.Equal(Error.Forbidden,(await service.ApproveAsync(1,7,role,Valid(),null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task InvalidInputNeverCallsRepository()
    {
        var repo=Substitute.For<IBookingApprovalRepository>();var service=new BookingApprovalService(repo);
        Assert.Equal(Error.Validation,(await service.ReadAsync(0,7,RoleCodes.Admin)).Error);
        Assert.Equal(Error.Validation,(await service.ApproveAsync(1,7,RoleCodes.Receptionist,Valid() with{Confirmed=false},null)).Error);
        Assert.Empty(repo.ReceivedCalls());
    }
    [Fact] public async Task NewAccountCannotGetJwtBeforeFirstOtp()
    {
        var users=Substitute.For<IUserRepository>();var hasher=Substitute.For<IPasswordHasher>();var tokens=Substitute.For<ITokenService>();
        var user=new User {UserId=7,Phone="0967000000",PasswordHash="synthetic-hash",RequiresPhoneActivation=true,Role=new Role{RoleCode=RoleCodes.Patient}};
        users.FindByPhoneAsync(user.Phone,default).Returns(user);hasher.Verify(user.Phone,user.PasswordHash).Returns(true);
        var svc=new AuthService(users,Substitute.For<IUnitOfWork>(),hasher,tokens,Substitute.For<IAuditLogger>(),Substitute.For<IPatientRepository>());
        var result=await svc.LoginAsync(new LoginRequest(user.Phone,user.Phone),null);
        Assert.Equal("AUTH_ACTIVATION_REQUIRED",result.Error.Code);Assert.Empty(tokens.ReceivedCalls());
    }
}
