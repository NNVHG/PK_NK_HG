using Dental.Application.Features.Appointments.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Appointments.Validators;

public sealed class AppointmentQueryRequestValidator : AbstractValidator<AppointmentQueryRequest>
{
    public AppointmentQueryRequestValidator()
    {
        RuleFor(x => x.Page)
            .GreaterThanOrEqualTo(1)
            .WithMessage("Trang phải lớn hơn hoặc bằng 1.");

        RuleFor(x => x.PageSize)
            .InclusiveBetween(1, 100)
            .WithMessage("Số lượng mỗi trang phải từ 1 đến 100.");
    }
}
