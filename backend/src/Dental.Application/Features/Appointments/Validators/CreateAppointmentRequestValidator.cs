using Dental.Application.Features.Appointments.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class CreateAppointmentRequestValidator : AbstractValidator<CreateAppointmentRequest>
{
    public CreateAppointmentRequestValidator()
    {
        RuleFor(x => x.DentistId).Null()
            .WithMessage("Không chọn hoặc chỉ định nha sĩ khi đặt lịch; nha sĩ trong phòng sẽ gọi số bệnh nhân.");
        RuleFor(x => x.PatientId)
            .GreaterThan(0)
            .WithMessage("Mã bệnh nhân không hợp lệ.");

        RuleFor(x => x.AppointmentDate)
            .NotEmpty()
            .WithMessage("Ngày hẹn không được để trống.");

        RuleFor(x => x.SlotTime)
            .Must(time => time.Ticks % TimeSpan.TicksPerMinute == 0)
            .WithMessage("Giờ hẹn phải chính xác đến phút; khung giờ được backend đối chiếu cấu hình hiện hành.");
        RuleFor(x => x.ExpectedScheduleRevision).Matches("^[A-F0-9]{64}$").When(x => x.ExpectedScheduleRevision is not null);
        RuleFor(x => x.CitizenId)
            .Must((request, value) => request.DocumentType == IdentityDocumentType.Passport
                ? value is { Length: >= 6 and <= 12 } && value.All(char.IsAsciiLetterOrDigit)
                : value is { Length: 12 } && value.All(char.IsAsciiDigit))
            .WithMessage("CCCD phải gồm 12 chữ số hoặc hộ chiếu phải gồm 6–12 chữ cái, chữ số.")
            .When(x => !string.IsNullOrWhiteSpace(x.CitizenId));
        RuleFor(x => x.DocumentType)
            .Must(value => !value.HasValue || Enum.IsDefined(value.Value))
            .WithMessage("Loại giấy tờ định danh không hợp lệ.");
        RuleFor(x => x.NationalityCode)
            .Matches("^[A-Za-z]{2,3}$")
            .WithMessage("Mã quốc gia cấp hộ chiếu phải gồm 2–3 chữ cái.")
            .When(x => x.DocumentType == IdentityDocumentType.Passport && !string.IsNullOrWhiteSpace(x.NationalityCode));
        RuleFor(x => x.NationalityCode)
            .Empty()
            .WithMessage("Chỉ nhập mã quốc gia khi dùng hộ chiếu.")
            .When(x => x.DocumentType != IdentityDocumentType.Passport);
        RuleFor(x => x.ContactPhone)
            .Matches("^0[0-9]{9}$")
            .WithMessage("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.")
            .When(x => !string.IsNullOrWhiteSpace(x.ContactPhone));
        RuleFor(x => x.GuardianFullName)
            .MaximumLength(100)
            .WithMessage("Họ tên người giám hộ không được vượt quá 100 ký tự.");
        RuleFor(x => x.GuardianRelationship)
            .MaximumLength(50)
            .WithMessage("Quan hệ với trẻ không được vượt quá 50 ký tự.");

        // DL337: reject retired fields, including payloads from older UI versions.
        RuleFor(x => x.IsEmergency).Equal(false).WithMessage("Phòng khám không tiếp nhận cấp cứu.");
        RuleFor(x => x.EmergencyReason).Null().WithMessage("Ghi điều cần lưu ý vào ghi chú.");
        RuleFor(x => x.ConfirmedDentistConflictAppointmentId).Null().WithMessage("Không hỗ trợ chen lịch.");
        RuleFor(x => x.ConfirmedDentistConflictRequestId).Null().WithMessage("Không hỗ trợ chen lịch.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));
    }
}
