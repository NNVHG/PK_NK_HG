using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
using Dental.Domain.Constants;
using Dental.Infrastructure.Services;
using NSubstitute;
using Xunit;
namespace Dental.Tests.Appointments;
public sealed class BookingRequestTests
{
    private sealed class Clock:TimeProvider{public DateTimeOffset Now=DateTimeOffset.UtcNow;public override DateTimeOffset GetUtcNow()=>Now;}
    private sealed class Sender:IBookingOtpDelivery{public bool IsConfigured{get;set;}=true;public string Code="";public bool Success=true;public Task<bool>SendAsync(string p,string c,CancellationToken ct){Code=c;return Task.FromResult(Success);}}
    private static BookingIntent Intent(Clock c)=>new("Synthetic patient","0967000000",new(1990,1,1),"Other",null,new VietnamClock(c).Today.AddDays(2),new(8,0),null,null,new string('A',64));
    [Fact]public void ValidIntentAndStrictLengths(){var c=new Clock();var i=Intent(c);var v=new BookingIntentValidator(new(c));Assert.True(v.Validate(i).IsValid);Assert.False(v.Validate(i with{Phone=i.Phone+"\n"}).IsValid);Assert.False(v.Validate(i with{ScheduleRevision=i.ScheduleRevision+"\n"}).IsValid);}
    [Theory][InlineData("name")][InlineData("phone")][InlineData("dob")][InlineData("gender")][InlineData("email")][InlineData("past")][InlineData("notes")][InlineData("dentist")][InlineData("patient")]
    public void InvalidIntentRejected(string field){var c=new Clock();var i=Intent(c);i=field switch{"name"=>i with{FullName=" "},"phone"=>i with{Phone="invalid"},"dob"=>i with{DateOfBirth=new VietnamClock(c).Today.AddDays(1)},"gender"=>i with{Gender="invalid"},"email"=>i with{Email="invalid"},"past"=>i with{AppointmentDate=new VietnamClock(c).Today.AddDays(-1)},"notes"=>i with{Notes=new string('X',501)},"dentist"=>i with{DentistId=0},_=>i with{PatientId=0}};Assert.False(new BookingIntentValidator(new(c)).Validate(i).IsValid);}
    [Fact]public async Task CodesAreRandomBoundedSingleUseAndPurposeBound(){var c=new Clock();var s=new Sender();using var otp=new BookingOtp(s,c);var i=Intent(c);var issued=await otp.IssueAsync(i,null,default);Assert.True(issued.IsSuccess);Assert.Matches("^[0-9]{6}$",s.Code);var req=new SubmitBookingRequest(i,issued.Value!.ChallengeId,s.Code,Guid.NewGuid(),true);Assert.False(otp.Consume(req with{Intent=i with{SlotTime=new(8,30)}},null));Assert.True(otp.Consume(req,null));Assert.False(otp.Consume(req,null));}
    [Fact]public async Task FiveWrongAttemptsLockCorrectCode(){var c=new Clock();var s=new Sender();using var otp=new BookingOtp(s,c);var i=Intent(c);var issued=await otp.IssueAsync(i,null,default);var correct=s.Code;var req=new SubmitBookingRequest(i,issued.Value!.ChallengeId,correct=="000000"?"111111":"000000",Guid.NewGuid(),true);for(var n=0;n<5;n++)Assert.False(otp.Consume(req,null));Assert.False(otp.Consume(req with{Code=correct},null));}
    [Fact]public async Task CodesExpireAtFiveMinutesAndResendWaitsSixtySeconds(){var c=new Clock();var s=new Sender();using var otp=new BookingOtp(s,c);var i=Intent(c);var one=await otp.IssueAsync(i,7,default);var old=s.Code;Assert.True((await otp.IssueAsync(i,7,default)).IsFailure);c.Now+=TimeSpan.FromSeconds(60);var two=await otp.IssueAsync(i,7,default);Assert.True(two.IsSuccess);Assert.False(otp.Consume(new(i,one.Value!.ChallengeId,old,Guid.NewGuid(),true),7));var code=s.Code;c.Now+=TimeSpan.FromMinutes(5);Assert.False(otp.Consume(new(i,two.Value!.ChallengeId,code,Guid.NewGuid(),true),7));}
    [Fact]public async Task CodeCannotMoveBetweenActors(){var c=new Clock();var s=new Sender();using var otp=new BookingOtp(s,c);var i=Intent(c);var issued=await otp.IssueAsync(i,7,default);var req=new SubmitBookingRequest(i,issued.Value!.ChallengeId,s.Code,Guid.NewGuid(),true);Assert.False(otp.Consume(req,null));Assert.True(otp.Consume(req,7));}
    [Fact]public async Task FailedDeliveryRevokesCode(){var c=new Clock();var s=new Sender{Success=false};using var otp=new BookingOtp(s,c);Assert.True((await otp.IssueAsync(Intent(c),null,default)).IsFailure);}
    [Theory][InlineData(RoleCodes.Patient)][InlineData(RoleCodes.Assistant)][InlineData(RoleCodes.Dentist)][InlineData(null)]
    public async Task ReviewIsStaffOnly(string? role){var c=new Clock();var repo=Substitute.For<IBookingRequestRepository>();var service=new BookingRequestService(repo,Substitute.For<IBookingOtp>(),new(c));Assert.Equal(Error.Forbidden,(await service.ListAsync(3,role,1)).Error);Assert.Equal(Error.Forbidden,(await service.RejectAsync(1,3,role,new(1,"reason",true),null)).Error);Assert.Empty(repo.ReceivedCalls());}
    [Fact]public async Task RejectionRequiresVersionReasonAndConfirmation(){var c=new Clock();var repo=Substitute.For<IBookingRequestRepository>();var service=new BookingRequestService(repo,Substitute.For<IBookingOtp>(),new(c));foreach(var req in new[]{new RejectBookingRequest(0,"reason",true),new(1," ",true),new(1,new string('A',501),true),new(1,"reason",false)})Assert.Equal(Error.Validation,(await service.RejectAsync(1,3,RoleCodes.Admin,req,null)).Error);Assert.Empty(repo.ReceivedCalls());}
    [Fact]public async Task InvalidSubmissionDoesNotConsumeOtp(){var c=new Clock();var repo=Substitute.For<IBookingRequestRepository>();var otp=Substitute.For<IBookingOtp>();var service=new BookingRequestService(repo,otp,new(c));var req=new SubmitBookingRequest(Intent(c),Guid.NewGuid(),"123456",Guid.NewGuid(),false);Assert.Equal(Error.Validation,(await service.SubmitAsync(req,null,null,null)).Error);Assert.Empty(repo.ReceivedCalls());Assert.Empty(otp.ReceivedCalls());}
    [Fact]public async Task ExactReplayDoesNotConsumeAnotherCode(){var c=new Clock();var repo=Substitute.For<IBookingRequestRepository>();var otp=Substitute.For<IBookingOtp>();var req=new SubmitBookingRequest(Intent(c),Guid.NewGuid(),"123456",Guid.NewGuid(),true);repo.ReplayAsync(req,null,default).Returns(new BookingReceipt(req.ClientRequestId,"Pending"));var service=new BookingRequestService(repo,otp,new(c));Assert.True((await service.SubmitAsync(req,null,null,null)).IsSuccess);Assert.Empty(otp.ReceivedCalls());}
    [Fact]public void PublicReceiptHasNoPatientIdentityOrPriority()=>Assert.Equal(new[]{"RequestId","Status"},typeof(BookingReceipt).GetProperties().Select(p=>p.Name).Order().ToArray());
    [Fact]
    public void SubmitBookingRequestDeserializesFromJsonSuccessfully()
    {
        const string json = """
        {
          "intent": {
            "fullName": "Nguyen Van A",
            "phone": "0912345678",
            "dateOfBirth": "1990-01-01",
            "gender": "Male",
            "email": null,
            "appointmentDate": "2026-10-26",
            "slotTime": "09:00:00",
            "dentistId": null,
            "notes": null,
            "scheduleRevision": "AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA",
            "patientId": null
          },
          "clientRequestId": "11111111-1111-1111-1111-111111111111",
          "confirmed": true
        }
        """;
        var options = new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        var request = System.Text.Json.JsonSerializer.Deserialize<SubmitBookingRequest>(json, options);
        Assert.NotNull(request);
        Assert.Equal("Nguyen Van A", request.Intent.FullName);
        Assert.Equal(Guid.Parse("11111111-1111-1111-1111-111111111111"), request.ClientRequestId);
        Assert.True(request.Confirmed);
    }
}
