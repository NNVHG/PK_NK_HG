using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.DentistId).GreaterThan(0).When(x => x.DentistId.HasValue)
            .WithMessage("Mã nha sĩ phải là số nguyên dương.");
        RuleFor(x => x.PatientId)
            .GreaterThan(0)
            .WithMessage("Mã bệnh nhân không hợp lệ.");

        RuleFor(x => x.AppointmentDate)
            .NotEmpty()
            .WithMessage("Ngày hẹn không được để trống.");

        RuleFor(x => x.SlotTime)
            .Must((request, time) => request.IsEmergency
                ? time.Ticks % TimeSpan.TicksPerMinute == 0
                : AppointmentSlots.IsValidSlot(time))
            .WithMessage("Giờ thường phải theo khung 30 phút; giờ cấp cứu được chọn theo phút.");

        RuleFor(x => x.EmergencyReason)
            .Must(reason => !string.IsNullOrWhiteSpace(reason) && reason.Trim().Length is >= 5 and <= 255)
            .When(x => x.IsEmergency)
            .WithMessage("Lý do cấp cứu phải có từ 5 đến 255 ký tự.");

        RuleFor(x => x.EmergencyReason)
            .Null()
            .When(x => !x.IsEmergency)
            .WithMessage("Chỉ nhập lý do cấp cứu khi bật lịch cấp cứu.");

        RuleFor(x => x.ConfirmedDentistConflictAppointmentId)
            .Null()
            .When(x => !x.IsEmergency)
            .WithMessage("Chỉ xác nhận chen ngang cho lịch cấp cứu.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
