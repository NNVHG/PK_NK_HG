using System.Reflection;
using Dental.Application.Common;
using Dental.Application.Features.Auth.DTOs;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Infrastructure;

namespace Dental.Api.Policies;

/// <summary>MOD_AUTH/Q-299: read the actual registered policies; labels describe them without granting permissions.</summary>
public sealed class RolePermissionMatrixProvider(IAuthorizationPolicyProvider policies) : IRolePermissionMatrixProvider
{
    private static readonly PermissionRoleResponse[] Roles = [
        new(RoleCodes.Admin, "Quản trị viên"), new(RoleCodes.Receptionist, "Lễ tân/Thu ngân"),
        new(RoleCodes.Dentist, "Nha sĩ"), new(RoleCodes.Assistant, "Phụ tá"), new(RoleCodes.Patient, "Bệnh nhân")];

    private static readonly IReadOnlyDictionary<string, (string Name, string Module, string Conditions)> Labels =
        new Dictionary<string, (string, string, string)>
        {
            [Policies.DentalImageRead] = ("Xem danh sách và tệp ảnh", "MOD_IMG", "Admin/Nha sĩ/Lễ tân hoặc Bệnh nhân sở hữu hồ sơ theo Q-247; stream JWT, không mở tệp công khai."),
            [Policies.DentalImageUpload] = ("Tải ảnh hồ sơ", "MOD_IMG", "Admin/Nha sĩ/Phụ tá; ca mở chưa thu tiền hoặc ảnh độc lập. Nha sĩ chỉ tải vào ca phụ trách."),
            [Policies.DentalImageManage] = ("Gán ảnh vào ca khám", "MOD_IMG", "Admin hoặc nha sĩ phụ trách; chỉ gán từ null vào ca mở/đính chính hợp lệ, chưa thu tiền theo Q-241/327."),
            [Policies.DoctorSignatureManage] = ("Quản lý ảnh chữ ký bác sĩ", "MOD_AUTH", "Admin quản lý; nha sĩ chỉ chữ ký của mình, tài khoản và nha sĩ đang hoạt động. Lưu phiên bản/audit theo Q316."),
            [Policies.PrescriptionHandover] = ("Bàn giao và hoàn tác thuốc", "MOD_RX", "Admin/Lễ tân/Phụ tá giao sau Paid; Admin hoặc chính người giao hoàn tác trong 30 phút, chỉ metadata và lý do/audit theo DL-298."),
            [Policies.PrescriptionWrite] = ("Kê đơn thuốc", "MOD_RX", "Chỉ nha sĩ phụ trách; Admin và các role hỗ trợ chỉ đọc. Không sửa đơn đã thu tiền/cấp phát/hủy; thuốc active/giá đã thiết lập."),
            [Policies.AdminOnly] = ("Quản trị hệ thống", "MOD_AUTH", "Chỉ Admin; từng thao tác còn kiểm tra ràng buộc dữ liệu."),
            [Policies.StaffAny] = ("Truy cập nghiệp vụ nhân viên", "MOD_AUTH", "Các nghiệp vụ nội bộ; không áp dụng cho tài khoản bệnh nhân."),
            [Policies.PatientManage] = ("Tạo hồ sơ hành chính bệnh nhân", "MOD_PAT", "Thông tin cá nhân do Lễ tân hoặc Admin quản lý."),
            [Policies.PatientEdit] = ("Sửa thông tin hành chính", "MOD_PAT", "Phụ tá không sửa tên, số điện thoại hoặc dữ liệu hành chính."),
            [Policies.PatientView] = ("Xem hồ sơ bệnh nhân", "MOD_PAT", "Bệnh nhân chỉ xem hồ sơ thuộc tài khoản của mình."),
            [Policies.MedicalHistoryWrite] = ("Ghi tiền sử bệnh lý", "MOD_PAT", "Còn kiểm tra lần khám và trạng thái hồ sơ."),
            [Policies.VitalSignsWrite] = ("Ghi sinh hiệu", "MOD_PAT", "Còn kiểm tra lần khám và trạng thái hồ sơ."),
            [Policies.PatientSafetyAlertsView] = ("Xem cảnh báo an toàn", "MOD_PAT", "Dữ liệu nội bộ trong phạm vi công việc của nhân viên."),
            [Policies.WorkspaceManage] = ("Vị trí nha sĩ và phụ tá", "MOD_MST", "Nha sĩ tự chọn ghế; phụ tá chọn nha sĩ rồi theo phòng; Admin can thiệp. Chặn đổi khi đang khám/gọi chưa xử lý (DL-261)."),
            [Policies.DiagnosisCatalogView] = ("Xem gợi ý chẩn đoán", "MOD_MST", "Admin quản lý danh mục; nha sĩ chỉ đọc mục đang dùng. Chẩn đoán lâm sàng vẫn nhập tự do."),
            [Policies.MedicineCatalogView] = ("Xem danh mục thuốc", "MOD_MST", "Admin quản lý danh mục và giá; nha sĩ chỉ đọc để kê đơn."),
            [Policies.StockIssueWrite] = ("Xác nhận tiêu hao thực tế theo ca", "MOD_INV", "Admin/Phụ tá xác nhận số nguyên thực tế theo ca hoàn tất; chưa gợi ý BOM; audit và trừ tồn cùng giao dịch (DL-315)."),
            [Policies.StockView] = ("Xem tồn kho vật tư", "MOD_INV", "Admin/Phụ tá xem tồn và bộ chọn nhập kho; Nha sĩ chỉ đọc vật tư đang dùng (DL-314)."),
            [Policies.StockReceiptWrite] = ("Lập và xem phiếu nhập kho", "MOD_INV", "Admin/Phụ tá xác nhận thủ công; cộng tồn và audit cùng giao dịch (DL-311/314)."),
            [Policies.MedicalSupplyCatalogView] = ("Xem danh mục vật tư tiêu hao", "MOD_MST", "Admin quản lý danh mục vật tư tiêu hao; nha sĩ chỉ đọc mục đang dùng (DL-070, DL-103, DL-167)."),
            [Policies.DentalChairView] = ("Xem danh mục phòng và ghế", "MOD_MST", "Admin/Nha sĩ/Phụ tá xem; chỉ Admin khai báo/sửa danh mục. Ghế bảo trì/ngừng dùng không phục vụ."),
            [Policies.ServiceCatalogView] = ("Xem danh mục dịch vụ", "MOD_MST", "Nha sĩ/Phụ tá được xem; tạo/sửa danh mục và bảng giá chỉ Admin."),
            [Policies.AppointmentCreate] = ("Đặt lịch khám", "MOD_APP", "Bệnh nhân chỉ đặt cho hồ sơ thuộc tài khoản của mình."),
            [Policies.AppointmentView] = ("Xem lịch hẹn", "MOD_APP", "Bệnh nhân chỉ xem lịch hẹn thuộc tài khoản của mình."),
            [Policies.AppointmentManage] = ("Quản lý lịch hẹn tại quầy", "MOD_APP", "Còn kiểm tra trạng thái và thời gian của lịch hẹn."),
            [Policies.AppointmentReschedule] = ("Đổi lịch hẹn", "MOD_APP", "Còn kiểm tra hồ sơ sở hữu, trạng thái và thời gian được đổi lịch."),
            [Policies.QueueCheckIn] = ("Tiếp đón, check-in và dọn lượt quá hạn", "MOD_CHK", "Lễ tân/Admin; dọn lượt chỉ Visit chưa bắt đầu, quá mốc 23:30 Việt Nam, có xác nhận (DL-260)."),
            [Policies.QueueView] = ("Xem hàng đợi nội bộ", "MOD_CHK", "Màn TV công khai là luồng đọc riêng với tên bệnh nhân đã che."),
            [Policies.QueueSkip] = ("Đánh dấu vắng mặt", "MOD_CHK", "Chỉ lượt Calling chưa khám; Nha sĩ chỉ thao tác lượt của mình. Gọi lại dành cho Lễ tân/Admin (DL-259)."),
            [Policies.QueueCall] = ("Gọi số mời vào phòng", "MOD_CHK", "Phụ tá gọi lượt đã có nha sĩ; gọi số không bắt đầu hoặc ghi ca khám. Bắt đầu khám chỉ Nha sĩ/Admin (DL-258)."),
            [Policies.QueueStatusUpdate] = ("Chuyển trạng thái hàng đợi", "MOD_CHK", "Mỗi bước chuyển kiểm tra quyền riêng: Nha sĩ khám/chỉ định chụp; Phụ tá hoàn tất chụp."),
            [Policies.ClinicalDiagnosisUpdate] = ("Khám và ghi chẩn đoán", "MOD_PAT", "Chỉ Admin hoặc nha sĩ phụ trách; lần khám đang mở, chưa khóa."),
            [Policies.FdiConditionWrite] = ("Ghi tình trạng răng", "MOD_FDI", "Chỉ Admin hoặc nha sĩ phụ trách; lần khám đang mở, chưa khóa."),
            [Policies.FdiServiceAssign] = ("Chỉ định dịch vụ điều trị", "MOD_FDI", "Chỉ Admin hoặc nha sĩ phụ trách; còn kiểm tra lần khám và dịch vụ đang dùng."),
            [Policies.InvoiceDraftView] = ("Xem hóa đơn theo lần khám", "MOD_BIL", "Bệnh nhân chỉ xem hóa đơn thuộc hồ sơ của mình."),
            [Policies.CashPaymentWrite] = ("Thu tiền và xem giao dịch", "MOD_BIL", "Khám đã hoàn tất; hóa đơn còn nợ; không thu vượt số còn phải trả."),
            [Policies.InvoiceDiscountWrite] = ("Áp dụng giảm giá", "MOD_BIL", "Lễ tân chỉ dùng mẫu trong hạn mức; giảm tùy chỉnh dành cho Admin; hóa đơn chưa thu tiền.")
        };

    public async Task<Result<RolePermissionMatrixResponse>> GetAsync(CancellationToken ct = default)
    {
        var rows = new List<RolePermissionResponse>();
        var codes = typeof(Policies).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Where(x => x.IsLiteral && x.FieldType == typeof(string)).Select(x => (string)x.GetRawConstantValue()!)
            .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal);
        foreach (var code in codes)
        {
            ct.ThrowIfCancellationRequested();
            var policy = await policies.GetPolicyAsync(code);
            var requirements = policy?.Requirements.OfType<RolesAuthorizationRequirement>().ToArray();
            // A new policy/requirement must be described before the read-only matrix can claim to represent it.
            if (policy is null || requirements is not { Length: > 0 } || !Labels.TryGetValue(code, out var label) ||
                policy.Requirements.Any(x => x is not (RolesAuthorizationRequirement or DenyAnonymousAuthorizationRequirement)) ||
                requirements.SelectMany(x => x.AllowedRoles).Any(role => !Roles.Any(x => x.Code == role)))
                return Result<RolePermissionMatrixResponse>.Failure(new Error("AUTH_050", "Ma trận quyền chưa đồng bộ cấu hình. Vui lòng liên hệ quản trị hệ thống."));
            var allowed = Roles.Where(role => requirements.All(rule => rule.AllowedRoles.Contains(role.Code, StringComparer.Ordinal)))
                .Select(role => role.Code).ToArray();
            rows.Add(new(code, label.Name, label.Module, allowed, label.Conditions));
        }
        return Result<RolePermissionMatrixResponse>.Success(new(Roles.ToArray(), rows.OrderBy(x => x.Module).ThenBy(x => x.Code).ToArray()));
    }
}
