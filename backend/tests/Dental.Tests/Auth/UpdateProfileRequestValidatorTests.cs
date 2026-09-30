using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Features.Auth.Validators;
using Xunit;

namespace Dental.Tests.Auth;

public sealed class UpdateProfileRequestValidatorTests
{
    private readonly UpdateProfileRequestValidator _validator = new();

    [Fact]
    public async Task ValidateAsync_InvalidProfileFields_ReturnsErrors()
    {
        var result = await _validator.ValidateAsync(
            new UpdateProfileRequest("   ", "12345", "not-an-email", new DateOnly(2030, 1, 1), "Không rõ"));

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateProfileRequest.FullName));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateProfileRequest.Phone));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateProfileRequest.Email));
        Assert.Contains(result.Errors, error => error.PropertyName == nameof(UpdateProfileRequest.Gender));
    }

    [Fact]
    public async Task ValidateAsync_ValidVietnamesePhoneAndOptionalEmail_ReturnsSuccess()
    {
        var result = await _validator.ValidateAsync(
            new UpdateProfileRequest("Nguyễn Văn A", "0912345678", null, new DateOnly(1998, 4, 12), "Female"));

        Assert.True(result.IsValid);
    }
}
