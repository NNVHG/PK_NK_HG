using Dental.Application.Features.MedicalHistory.DTOs;
using FluentValidation;

namespace Dental.Application.Features.MedicalHistory.Validators;

public sealed class RecordMedicalHistoryRequestValidator : AbstractValidator<RecordMedicalHistoryRequest>
{
    public RecordMedicalHistoryRequestValidator()
    {
        RuleFor(request => request.Note)
            .MaximumLength(2000).WithMessage("Ghi chú không được vượt quá 2000 ký tự.");

        RuleFor(request => request.Items)
            .NotNull().WithMessage("Danh sách tiền sử không được để trống.")
            .Must(items => items is null || items.Count <= 50)
            .WithMessage("Mỗi bản chụp chỉ được có tối đa 50 mục.")
            .Must(HaveUniqueNames)
            .WithMessage("Tên mục không được trùng trong cùng một bản chụp.");

        RuleForEach(request => request.Items)
            .SetValidator(new MedicalHistoryItemRequestValidator());
    }

    private static bool HaveUniqueNames(IReadOnlyCollection<MedicalHistoryItemRequest>? items)
    {
        if (items is null)
            return true;

        var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        return items.All(item => !string.IsNullOrWhiteSpace(item.Name) && names.Add(item.Name.Trim()));
    }
}
