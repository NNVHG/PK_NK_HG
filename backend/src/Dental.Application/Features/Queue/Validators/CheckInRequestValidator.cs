using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class CheckInRequestValidator : AbstractValidator<CheckInRequest>
{
    public CheckInRequestValidator()
    {
        RuleFor(x => x.PatientId)
            .GreaterThan(0)
            .WithMessage("Mã bệnh nhân không hợp lệ.");

        RuleFor(x => x.Notes)
            .MaximumLength(500)
            .WithMessage("Ghi chú không được vượt quá 500 ký tự.")
            .When(x => !string.IsNullOrEmpty(x.Notes));

        RuleFor(x => x.CitizenId)
            .NotEmpty().WithMessage("Vui lòng nhập CCCD để đối chiếu tại quầy.")
            .Length(12).Matches("^[0-9]{12}$")
            .WithMessage("CCCD phải gồm 12 chữ số.");

        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Vui lòng nhập số điện thoại để đối chiếu tại quầy.")
            .Length(10).Matches("^0[0-9]{9}$")
            .WithMessage("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.");
    }
}
