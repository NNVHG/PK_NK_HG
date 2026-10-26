using System.Text.RegularExpressions;
using Dental.Application.Features.Auth.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using FluentValidation;

namespace Dental.Application.Features.Auth.Validators;

public sealed class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Phone)
            .NotEmpty().WithMessage("Nhập SĐT, CCCD hoặc số Hộ chiếu.")
            .Must((req, val) =>
            {
                if (string.IsNullOrWhiteSpace(val)) return false;
                val = val.Trim();

                if (req.DocumentType == IdentityDocumentType.Passport)
                {
                    return Regex.IsMatch(val, IdentityDocumentConstants.PassportNumberRegex);
                }

                // Mặc định hoặc CCCD: SĐT 10 số, CCCD 12 số, hoặc số Passport hợp lệ
                return Regex.IsMatch(val, @"^(0\d{9}|\d{12})$") ||
                       (req.DocumentType.HasValue && req.DocumentType.Value == IdentityDocumentType.Passport && Regex.IsMatch(val, IdentityDocumentConstants.PassportNumberRegex));
            })
            .WithMessage("Nhập SĐT gồm 10 số, CCCD gồm 12 số hoặc Hộ chiếu 6–12 ký tự hợp lệ.");

        When(x => x.DocumentType == IdentityDocumentType.Passport, () =>
        {
            RuleFor(x => x.NationalityCode)
                .NotEmpty().WithMessage("Vui lòng cung cấp mã quốc gia cấp hộ chiếu.")
                .Matches(IdentityDocumentConstants.NationalityCodeRegex).WithMessage("Mã quốc gia phải gồm 2–3 chữ cái.");
        });

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage("Mật khẩu không được để trống.")
            .MinimumLength(6).WithMessage("Mật khẩu phải có ít nhất 6 ký tự.");
    }
}

