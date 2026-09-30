using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Validators;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class AuditLogQueryRequestValidatorTests
{
    private readonly AuditLogQueryRequestValidator _validator = new();

    [Fact]
    public void Validate_WhenDateRangeIsReversed_ReturnsVietnameseError()
    {
        var result = _validator.Validate(new AuditLogQueryRequest
        {
            FromDate = new DateOnly(2026, 10, 1),
            ToDate = new DateOnly(2026, 9, 30),
        });

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.ErrorMessage == "Ngày bắt đầu phải trước hoặc bằng ngày kết thúc.");
    }
}
