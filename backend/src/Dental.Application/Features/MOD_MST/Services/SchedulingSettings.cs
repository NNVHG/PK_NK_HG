using Dental.Application.Common;
using Dental.Domain.Constants;
using FluentValidation;
namespace Dental.Application.Features.MOD_MST.Services;

public sealed record ScheduleSnapshot(ClinicSchedule Schedule, string Revision, bool UsingFallback);
public sealed record SchedulingSettingsRequest(string WorkStartTime, string LunchStartTime, string LunchEndTime, string WorkEndTime, int SlotDurationMinutes, string ExpectedRevision);
public sealed record SchedulingSettingsDto(string WorkStartTime, string LunchStartTime, string LunchEndTime, string WorkEndTime, int SlotDurationMinutes, string Revision, bool UsingFallback);
public sealed record BookingScheduleDto(string[] Slots, int SlotDurationMinutes, string Revision);
public interface ISchedulingSettingsRepository
{
    Task<Result<ScheduleSnapshot>> ReadAsync(int actor, CancellationToken ct);
    Task<Result<ScheduleSnapshot>> SaveAsync(int actor, SchedulingSettingsRequest request, string? ip, CancellationToken ct);
}
public sealed class SchedulingSettingsValidator : AbstractValidator<SchedulingSettingsRequest>
{
    public SchedulingSettingsValidator()
    {
        RuleFor(x => x.ExpectedRevision).NotEmpty().Matches("^[A-F0-9]{64}$").WithMessage("Cần tải lại phiên bản cấu hình.");
        RuleFor(x => x).Must(x => ClinicSchedule.TryParse(x.WorkStartTime, x.LunchStartTime, x.LunchEndTime, x.WorkEndTime, x.SlotDurationMinutes, out _))
            .WithMessage("Giờ phải theo HH:mm, mở cửa < nghỉ trưa < hết nghỉ < đóng cửa; thời lượng nguyên phút và có ít nhất một slot trọn trong buổi.");
    }
}
public sealed class SchedulingSettingsService(ISchedulingSettingsRepository repository)
{
    public static SchedulingSettingsDto Dto(ScheduleSnapshot x) => new(x.Schedule.WorkStart.ToString("HH:mm"), x.Schedule.LunchStart.ToString("HH:mm"), x.Schedule.LunchEnd.ToString("HH:mm"), x.Schedule.WorkEnd.ToString("HH:mm"), x.Schedule.DurationMinutes, x.Revision, x.UsingFallback);
    public async Task<Result<SchedulingSettingsDto>> ReadAsync(int? actor, string? role, CancellationToken ct = default)
    {
        if (actor is not > 0) return Result<SchedulingSettingsDto>.Failure(Error.Unauthorized);
        if (role != RoleCodes.Admin) return Result<SchedulingSettingsDto>.Failure(Error.Forbidden);
        var r = await repository.ReadAsync(actor.Value, ct);
        return r.IsSuccess ? Result<SchedulingSettingsDto>.Success(Dto(r.Value!)) : Result<SchedulingSettingsDto>.Failure(r.Error);
    }
    public async Task<Result<SchedulingSettingsDto>> SaveAsync(int? actor, string? role, SchedulingSettingsRequest request, string? ip, CancellationToken ct = default)
    {
        if (actor is not > 0) return Result<SchedulingSettingsDto>.Failure(Error.Unauthorized);
        if (role != RoleCodes.Admin) return Result<SchedulingSettingsDto>.Failure(Error.Forbidden);
        var valid = new SchedulingSettingsValidator().Validate(request);
        if (!valid.IsValid) return Result<SchedulingSettingsDto>.Failure(new Error("MST_SCHEDULE", valid.Errors[0].ErrorMessage));
        var r = await repository.SaveAsync(actor.Value, request, ip, ct);
        return r.IsSuccess ? Result<SchedulingSettingsDto>.Success(Dto(r.Value!)) : Result<SchedulingSettingsDto>.Failure(r.Error);
    }
}
