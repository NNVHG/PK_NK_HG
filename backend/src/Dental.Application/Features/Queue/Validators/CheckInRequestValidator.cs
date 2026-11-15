using Dental.Application.Features.Queue.DTOs;
using Dental.Domain.Enums;
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

        RuleFor(x => x.DocumentType)
            .Must(value => value is null or IdentityDocumentType.CitizenId or IdentityDocumentType.Passport)
            .WithMessage("Loại giấy tờ định danh không hợp lệ.");

        RuleFor(x => x.GuardianFullName)
            .MaximumLength(100)
            .WithMessage("Họ tên người giám hộ không được vượt quá 100 ký tự.");

        RuleFor(x => x.GuardianRelationship)
            .MaximumLength(50)
            .WithMessage("Quan hệ với trẻ không được vượt quá 50 ký tự.");

        When(x => x.DocumentType == IdentityDocumentType.Passport, () =>
        {
            RuleFor(x => x.CitizenId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Vui lòng nhập số hộ chiếu để đối chiếu tại quầy.")
                .Matches("\\A[A-Za-z0-9]{6,12}\\z")
                .WithMessage("Số hộ chiếu phải gồm 6–12 chữ cái hoặc chữ số.");
            RuleFor(x => x.NationalityCode)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Vui lòng nhập quốc gia cấp hộ chiếu.")
                .Matches("\\A[A-Za-z]{2,3}\\z")
                .WithMessage("Mã quốc gia cấp hộ chiếu phải gồm 2–3 chữ cái.");
        });

        When(x => x.DocumentType is null or IdentityDocumentType.CitizenId, () =>
        {
            RuleFor(x => x.CitizenId)
                .Cascade(CascadeMode.Stop)
                .NotEmpty().WithMessage("Vui lòng nhập CCCD để đối chiếu tại quầy.")
                .Matches("\\A[0-9]{12}\\z")
                .WithMessage("CCCD phải gồm 12 chữ số.")
                .When(HasCitizenIdInput);
            RuleFor(x => x.NationalityCode)
                .Must(string.IsNullOrWhiteSpace)
                .WithMessage("CCCD không cần mã quốc gia cấp hộ chiếu.");
        });

        RuleFor(x => x.Phone)
            .Cascade(CascadeMode.Stop)
            .NotEmpty().WithMessage("Vui lòng nhập số điện thoại để đối chiếu tại quầy.")
            .Matches("\\A0[0-9]{9}\\z")
            .WithMessage("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.")
            .When(HasIdentityDocumentInput);

        RuleFor(x => x.Phone)
            .Matches("\\A0[0-9]{9}\\z")
            .WithMessage("Số điện thoại phải gồm 10 chữ số và bắt đầu bằng 0.")
            .When(x => !HasIdentityDocumentInput(x) && !string.IsNullOrWhiteSpace(x.Phone));
    }

    private static bool HasIdentityDocumentInput(CheckInRequest request) =>
        request.DocumentType.HasValue || HasCitizenIdInput(request) || !string.IsNullOrWhiteSpace(request.NationalityCode);

    private static bool HasCitizenIdInput(CheckInRequest request) => !string.IsNullOrWhiteSpace(request.CitizenId);
}
