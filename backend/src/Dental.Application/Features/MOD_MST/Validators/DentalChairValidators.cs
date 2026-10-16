using Dental.Application.Features.MOD_MST.DTOs;
using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Features.MOD_MST.Validators;

public sealed class DentalChairQueryValidator : AbstractValidator<DentalChairQuery>
{
    public DentalChairQueryValidator()
    {
        RuleFor(x => x.Keyword).MaximumLength(100);
        RuleFor(x => x.Status).Must(s => !s.HasValue || Enum.IsDefined(typeof(DentalChairStatus), s.Value));
        RuleFor(x => x.Page).InclusiveBetween(1, 1_000_000); RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
public sealed class CreateDentalChairRequestValidator : AbstractValidator<CreateDentalChairRequest>
{
    public CreateDentalChairRequestValidator()
    {
        RuleFor(x => x.ChairNumber).NotEmpty().MaximumLength(50);
        RuleFor(x => x.RoomName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Status).Must(s => s is 1 or 3 or 4).WithMessage("Trạng thái Đang phục vụ do ca khám quản lý.");
        RuleFor(x => x.Confirmed).Equal(true);
    }
}
public sealed class UpdateDentalChairRequestValidator : AbstractValidator<UpdateDentalChairRequest>
{
    public UpdateDentalChairRequestValidator()
    {
        RuleFor(x => x.RoomName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Description).MaximumLength(500);
        RuleFor(x => x.Status).Must(s => s is 1 or 3 or 4).WithMessage("Trạng thái Đang phục vụ do ca khám quản lý.");
        RuleFor(x => x.ExpectedVersion).GreaterThan(0); RuleFor(x => x.Confirmed).Equal(true);
    }
}
