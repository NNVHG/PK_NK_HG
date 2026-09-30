namespace Dental.Application.Features.Staff.DTOs;

// [CẦN XÁC NHẬN] Chưa có cờ buộc đổi mật khẩu lần đầu trong User; Admin nhập mật khẩu thủ công.
public sealed record CreateStaffRequest(
    string FullName,
    string Phone,
    string Password,
    string RoleCode,
    string? Email,
    DateOnly? DateOfBirth,
    string? Gender);
