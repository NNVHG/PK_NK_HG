using Dental.Application.Features.Visits.DTOs;
using FluentValidation;

namespace Dental.Application.Features.Visits.Validators;

public sealed class CreatePostTreatmentNoteRequestValidator : AbstractValidator<CreatePostTreatmentNoteRequest>
{
    public CreatePostTreatmentNoteRequestValidator()
    {
        RuleFor(x => x.RequestId).Must(id => id != Guid.Empty).WithMessage("Mã yêu cầu không hợp lệ.");
        RuleFor(x => x.Note)
            .Cascade(CascadeMode.Stop)
            .NotEmpty()
            .WithMessage("Nội dung ghi chú hậu điều trị không được để trống.")
            .Must(x => !string.IsNullOrWhiteSpace(x))
            .WithMessage("Nội dung ghi chú hậu điều trị không được chỉ chứa khoảng trắng.")
            .MaximumLength(1000)
            .WithMessage("Nội dung ghi chú hậu điều trị không được vượt quá 1000 ký tự.");
    }
}
