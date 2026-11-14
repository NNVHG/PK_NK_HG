using Dental.Application.Features.Queue.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Queue.Validators;

public sealed class PauseVisitRequestValidator : AbstractValidator<PauseVisitRequest>
{
    public PauseVisitRequestValidator()
    {
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Vui lòng xác nhận tạm dừng ca khám.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Lý do không được vượt quá 500 ký tự.");
    }
}

public sealed class ResumeVisitRequestValidator : AbstractValidator<ResumeVisitRequest>
{
    public ResumeVisitRequestValidator()
    {
        RuleFor(x => x.Confirmed).Equal(true).WithMessage("Vui lòng xác nhận tiếp tục ca khám.");
        RuleFor(x => x.DentistId).GreaterThan(0).When(x => x.DentistId.HasValue).WithMessage("Nha sĩ không hợp lệ.");
        RuleFor(x => x.Reason).MaximumLength(500).WithMessage("Ghi chú không được vượt quá 500 ký tự.");
    }
}
