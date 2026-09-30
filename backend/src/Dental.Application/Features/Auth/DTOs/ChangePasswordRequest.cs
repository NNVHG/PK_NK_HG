namespace Dental.Application.Features.Auth.DTOs;

public sealed record ChangePasswordRequest(
    string OldPassword,
    string NewPassword
);
