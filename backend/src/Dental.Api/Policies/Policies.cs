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
    public const string ServiceCatalogView = "ServiceCatalogView";
    public const string AppointmentCreate = "AppointmentCreate";
    public const string AppointmentView = "AppointmentView";
    public const string AppointmentManage = "AppointmentManage";
    public const string QueueCheckIn = "QueueCheckIn";
    public const string QueueView = "QueueView";
    public const string QueueStatusUpdate = "QueueStatusUpdate";
    public const string ClinicalDiagnosisUpdate = "ClinicalDiagnosisUpdate";
    public const string FdiConditionWrite = "FdiConditionWrite";
    public const string FdiServiceAssign = "FdiServiceAssign";
    public const string InvoiceDraftView = "InvoiceDraftView";
    public const string CashPaymentWrite = "CashPaymentWrite";


    public static void AddApplicationPolicies(this AuthorizationOptions opts)
    {
        opts.AddPolicy(CashPaymentWrite, policy => policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist));
        opts.AddPolicy(FdiConditionWrite, policy => policy.RequireRole(RoleCodes.Admin, RoleCodes.Dentist));
        opts.AddPolicy(FdiServiceAssign, policy => policy.RequireRole(RoleCodes.Admin, RoleCodes.Dentist));
        opts.AddPolicy(InvoiceDraftView, policy => policy.RequireRole(RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Receptionist, RoleCodes.Patient));
        opts.AddPolicy(AdminOnly, policy =>
            policy.RequireRole(RoleCodes.Admin));

        opts.AddPolicy(StaffAny, policy =>
            policy.RequireRole(RoleCodes.StaffRoles));

        // DL-023: chỉ Admin và Lễ tân được tạo thông tin cá nhân bệnh nhân.
        opts.AddPolicy(PatientManage, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Admin));

        opts.AddPolicy(PatientView, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Admin, RoleCodes.Patient));

        // DL-023: sửa thông tin cá nhân chỉ dành Lễ tân và Admin.
        opts.AddPolicy(PatientEdit, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Admin));

        opts.AddPolicy(MedicalHistoryWrite, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Admin));

        opts.AddPolicy(VitalSignsWrite, policy =>
            policy.RequireRole(RoleCodes.Receptionist, RoleCodes.Assistant, RoleCodes.Admin));

        opts.AddPolicy(PatientSafetyAlertsView, policy =>
            policy.RequireRole(RoleCodes.StaffRoles));

        opts.AddPolicy(ServiceCatalogView, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Dentist, RoleCodes.Assistant));

        // DL-011, DL-043: Đặt lịch trực tuyến (Patient) hoặc tại quầy (Receptionist, Admin)
        opts.AddPolicy(AppointmentCreate, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Patient));

        opts.AddPolicy(AppointmentView, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient));

        opts.AddPolicy(AppointmentManage, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist));

        // DL-044, DL-049: Tiếp đón và cấp số hàng đợi
        opts.AddPolicy(QueueCheckIn, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist));

        // DL-047: Nhân viên xem danh sách hàng đợi
        opts.AddPolicy(QueueView, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant));

        // DL-047, DL-053: Cập nhật trạng thái hàng đợi (chuyển trạng thái chi tiết kiểm tra theo vai trò trong service)
        opts.AddPolicy(QueueStatusUpdate, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant));

        // DL-085: Sửa chẩn đoán và ghi chú lâm sàng: Nha sĩ và Admin (kiểm tra Dentist phụ trách tại service)
        opts.AddPolicy(ClinicalDiagnosisUpdate, policy =>
            policy.RequireRole(RoleCodes.Admin, RoleCodes.Dentist));
    }
}
