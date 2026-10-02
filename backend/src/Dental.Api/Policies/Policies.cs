using Dental.Domain.Constants;
using Microsoft.AspNetCore.Authorization;

namespace Dental.Api.Policies;

/// <summary>
/// Authorization policies được đặt tên rõ ràng — dùng [Authorize(Policy = Policies.AdminOnly)].
/// Tránh dùng [Authorize(Roles = "ADMIN")] string thô.
/// </summary>
public static class Policies
{
    public const string AdminOnly  = "AdminOnly";
    public const string StaffAny   = "StaffAny";    // Admin, Lễ tân, Nha sĩ, Phụ tá
    public const string PatientManage = "PatientManage";
    public const string PatientView = "PatientView";
    public const string PatientEdit = "PatientEdit";
    public const string MedicalHistoryWrite = "MedicalHistoryWrite";
    public const string VitalSignsWrite = "VitalSignsWrite";
    public const string PatientSafetyAlertsView = "PatientSafetyAlertsView";

    public static void AddApplicationPolicies(this AuthorizationOptions opts)
    {
        opts.AddPolicy(AdminOnly, policy =>
            policy.RequireRole(RoleCodes.Admin));

        opts.AddPolicy(StaffAny, policy =>
            policy.RequireRole(RoleCodes.StaffRoles));

        // [CẦN XÁC NHẬN] Sheet 05 chỉ cho Phụ tá xem hồ sơ theo nghiệp vụ, còn F_PAT_01 liệt kê Phụ tá được tạo hồ sơ.
        opts.AddPolicy(PatientManage, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Admin));

        opts.AddPolicy(PatientView, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Admin, RoleCodes.Patient));

        // [CẦN XÁC NHẬN] F_PAT_01 cho Phụ tá tạo hồ sơ, còn ma trận chỉ cho Phụ tá xem; sửa hồ sơ theo ma trận chỉ dành Lễ tân và Admin.
        opts.AddPolicy(PatientEdit, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Admin));

        opts.AddPolicy(MedicalHistoryWrite, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Admin));

        opts.AddPolicy(VitalSignsWrite, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Admin));

        opts.AddPolicy(PatientSafetyAlertsView, policy =>
            policy.RequireRole(RoleCodes.StaffRoles));
    }
}
