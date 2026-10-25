using System.Text.Json.Serialization;
using Dental.Application.Common;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_APP;

public sealed record BookingIntent(string FullName,string Phone,DateOnly DateOfBirth,string Gender,string? Email,
    DateOnly AppointmentDate,TimeOnly SlotTime,int? DentistId,string? Notes,string ScheduleRevision,int? PatientId=null);
public sealed record BookingOtpRequest(BookingIntent Intent);
public sealed record BookingOtpResponse(Guid ChallengeId,int ExpiresInSeconds=300,int ResendAfterSeconds=60);
[method: JsonConstructor]
public sealed record SubmitBookingRequest(BookingIntent Intent,Guid ClientRequestId,bool Confirmed)
{
    // Compatibility constructor for existing internal callers; appointment submission no longer uses OTP.
    public SubmitBookingRequest(BookingIntent intent,Guid challengeId,string code,Guid clientRequestId,bool confirmed)
        :this(intent,clientRequestId,confirmed){ChallengeId=challengeId;Code=code;}
    public Guid ChallengeId { get; init; }
    public string Code { get; init; } = string.Empty;
}
public sealed record BookingReceipt(Guid RequestId,string Status);
public sealed record PublicBookingDentist(int DentistId,string FullName);
public sealed record PublicBookingCatalog(string Today,string[] Slots,int DurationMinutes,string Revision,PublicBookingDentist[] Dentists);
public sealed record BookingRequestRow(int Id,string FullName,string Phone,DateOnly DateOfBirth,DateOnly AppointmentDate,
    TimeOnly SlotTime,int SlotDurationMinutes,int? DentistId,string? DentistName,string? Notes,bool IsReturning,DateTime CreatedAt,int Version);
public sealed record RejectBookingRequest(int ExpectedVersion,string Reason,bool Confirmed);
public sealed record PendingDentistConflict(int RequestId,string DentistName,DateOnly AppointmentDate,TimeOnly SlotTime);
public interface IBookingRequestRepository
{
    Task<PublicBookingCatalog> CatalogAsync(CancellationToken ct);
    Task<Result<bool>> ValidateAsync(BookingIntent intent,int? actor,string? role,CancellationToken ct);
    Task<BookingReceipt?> ReplayAsync(SubmitBookingRequest request,int? actor,CancellationToken ct);
    Task<Result<BookingReceipt>> SubmitAsync(SubmitBookingRequest request,int? actor,string? role,string? ip,CancellationToken ct);
    Task<Result<PagedResult<BookingRequestRow>>> ListAsync(int actor,string role,int page,CancellationToken ct);
    Task<Result<bool>> RejectAsync(int id,int actor,string role,RejectBookingRequest request,string? ip,CancellationToken ct);
}
public interface IBookingOtp
{
    bool IsConfigured { get; }
    Task<Result<BookingOtpResponse>> IssueAsync(BookingIntent intent,int? actor,CancellationToken ct);
    bool Consume(SubmitBookingRequest request,int? actor);
}
public sealed class BookingIntentValidator:AbstractValidator<BookingIntent>
{
    public BookingIntentValidator(VietnamClock clock)
    {
        RuleFor(x=>x.FullName).NotEmpty().MaximumLength(100).Must(x=>x is not null&&!x.Contains('\0'));
        RuleFor(x=>x.Phone).NotEmpty().Length(10).Matches("^0[0-9]{9}$");
        RuleFor(x=>x.DateOfBirth).Must(d=>d!=default&&d<=clock.Today&&d>=clock.Today.AddYears(-120));
        RuleFor(x=>x.Gender).Must(x=>x is "Male" or "Female" or "Other");
        RuleFor(x=>x.Email).MaximumLength(100).EmailAddress().When(x=>!string.IsNullOrEmpty(x.Email));
        RuleFor(x=>x.Notes).MaximumLength(500).Must(x=>x is null||!x.Contains('\0'));
        RuleFor(x=>x.AppointmentDate).Must(d=>d>=clock.Today);
        RuleFor(x=>x.SlotTime).Must(t=>t.Ticks%TimeSpan.TicksPerMinute==0);
        RuleFor(x=>x.DentistId).GreaterThan(0).When(x=>x.DentistId.HasValue);
        RuleFor(x=>x.PatientId).GreaterThan(0).When(x=>x.PatientId.HasValue);
        RuleFor(x=>x.ScheduleRevision).NotEmpty().Length(64).Matches("^[A-F0-9]{64}$");
    }
}
public sealed class BookingRequestService(IBookingRequestRepository repository,IBookingOtp otp,VietnamClock clock)
{
    public static readonly Error InvalidOtp=new("APP_REQUEST_OTP","Mã xác thực không đúng, hết hạn hoặc đã dùng. Yêu cầu mã mới khi cần.");
    private bool Valid(BookingIntent? intent)=>intent is not null&&new BookingIntentValidator(clock).Validate(intent).IsValid;
    public Task<PublicBookingCatalog> CatalogAsync(CancellationToken ct=default)=>repository.CatalogAsync(ct);
    public async Task<Result<BookingOtpResponse>> RequestOtpAsync(BookingOtpRequest request,int? actor,string? role,CancellationToken ct=default)
    {
        if(!Valid(request.Intent))return Result<BookingOtpResponse>.Failure(Error.Validation);
        if(!otp.IsConfigured)return Result<BookingOtpResponse>.Failure(new Error("APP_REQUEST_UNAVAILABLE","Kênh xác thực đặt lịch chưa sẵn sàng. Vui lòng liên hệ phòng khám."));
        var valid=await repository.ValidateAsync(request.Intent,actor,role,ct);
        return valid.IsFailure?Result<BookingOtpResponse>.Failure(valid.Error):await otp.IssueAsync(request.Intent,actor,ct);
    }
    public async Task<Result<BookingReceipt>> SubmitAsync(SubmitBookingRequest request,int? actor,string? role,string? ip,CancellationToken ct=default)
    {
        if(!Valid(request.Intent)||!request.Confirmed||request.ClientRequestId==Guid.Empty)return Result<BookingReceipt>.Failure(Error.Validation);
        var replay=await repository.ReplayAsync(request,actor,ct);
        if(replay is not null)return Result<BookingReceipt>.Success(replay);
        return await repository.SubmitAsync(request,actor,role,ip,ct);
    }
    public Task<Result<PagedResult<BookingRequestRow>>> ListAsync(int actor,string? role,int page,CancellationToken ct=default)
        =>actor<=0||role is not(RoleCodes.Admin or RoleCodes.Receptionist)?Task.FromResult(Result<PagedResult<BookingRequestRow>>.Failure(Error.Forbidden))
        :page is <1 or >1000000?Task.FromResult(Result<PagedResult<BookingRequestRow>>.Failure(Error.Validation)):repository.ListAsync(actor,role,page,ct);
    public Task<Result<bool>> RejectAsync(int id,int actor,string? role,RejectBookingRequest request,string? ip,CancellationToken ct=default)
        =>actor<=0||role is not(RoleCodes.Admin or RoleCodes.Receptionist)?Task.FromResult(Result<bool>.Failure(Error.Forbidden))
        :id<=0||request.ExpectedVersion<1||request.ExpectedVersion==int.MaxValue||!request.Confirmed||string.IsNullOrWhiteSpace(request.Reason)||request.Reason.Length>500||request.Reason.Contains('\0')?Task.FromResult(Result<bool>.Failure(Error.Validation))
        :repository.RejectAsync(id,actor,role,request,ip,ct);
}
