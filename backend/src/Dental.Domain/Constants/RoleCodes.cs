namespace Dental.Domain.Constants;

/// <summary>
/// Mã vai trò (không dấu) dùng trong JWT claim và [Authorize(Roles=...)].
/// RoleName tiếng Việt chỉ để hiển thị UI.
/// </summary>
public static class RoleCodes
{
    public const string Admin        = "ADMIN";
    public const string Receptionist = "RECEPTIONIST";
    public const string Dentist      = "DENTIST";
    public const string Assistant    = "ASSISTANT";
    public const string Patient      = "PATIENT";

    /// <summary>Tất cả vai trò nhân viên phòng khám (trừ bệnh nhân).</summary>
    public static readonly string[] StaffRoles =
        [Admin, Receptionist, Dentist, Assistant];
}

/// <summary>
/// Tên policy dùng trong [Authorize(Policy=...)] và AddAuthorization().
/// </summary>
public static class Policies
{
    public const string AdminOnly  = "AdminOnly";
    public const string StaffAny   = "StaffAny";     // bất kỳ nhân viên
    public const string DentistOrAdmin = "DentistOrAdmin";
}
