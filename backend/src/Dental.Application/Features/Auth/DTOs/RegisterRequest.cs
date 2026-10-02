namespace Dental.Application.Features.Auth.DTOs;

public sealed record RegisterRequest(
    string FullName,
    string Phone,
    string Password,
    DateOnly DateOfBirth,
    string Gender,
    string? Email);
