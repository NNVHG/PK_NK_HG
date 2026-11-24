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
            [Policies.ChairStatusManage] = ("Xem và sửa trạng thái phòng điều trị", "MOD_ASS", "Trạng thái phòng tự động cập nhật theo ca khám; chỉ Admin được xem và sửa khi cần nếu phòng không có nha sĩ hoặc ca InProgress (DL-317)."),
            [Policies.DentalImageRead] = ("Xem danh sách và tệp ảnh", "MOD_IMG", "Admin/Nha sĩ/Lễ tân hoặc Bệnh nhân sở hữu hồ sơ theo Q-247; stream JWT, không mở tệp công khai."),
            [Policies.DentalImageUpload] = ("Tải ảnh hồ sơ", "MOD_IMG", "Admin/Nha sĩ/Phụ tá; ca mở chưa thu tiền hoặc ảnh độc lập. Nha sĩ chỉ tải vào ca phụ trách."),
            [Policies.DentalImageManage] = ("Gán ảnh và sửa thông tin ảnh", "MOD_IMG", "Admin hoặc nha sĩ phụ trách; chỉ gán từ null theo Q-241/327. Metadata Notes/Category/ToothNumber chỉ InProgress/Correcting hợp lệ chưa khóa/chưa thu, ảnh độc lập chỉ Admin (DL-324). Clinical Completed chỉ bổ sung theo DL-244; không sửa gốc."),
            [Policies.DentalImageDelete] = ("Xóa mềm ảnh hồ sơ", "MOD_IMG", "Admin hoặc chính Nha sĩ/Phụ tá đã tải ảnh theo DL-323; ảnh độc lập hoặc ca InProgress chưa khóa/chưa thu. Metadata xóa không cấp quyền đọc tệp/clinical cho Phụ tá. Bắt buộc lý do, xác nhận, phiên bản và audit; giữ tệp vật lý."),
            [Policies.PrescriptionHandover] = ("Bàn giao và hoàn tác thuốc", "MOD_RX", "Admin/Lễ tân/Phụ tá giao theo DL-301, kiểm/trừ tồn cùng giao dịch theo DL-318. Admin hoặc chính người giao hoàn tác trong 30 phút, chỉ metadata/lý do/audit, không tự cộng kho theo DL-298."),
            [Policies.PrescriptionWrite] = ("Kê đơn thuốc", "MOD_RX", "Chỉ nha sĩ phụ trách; Admin và các role hỗ trợ chỉ đọc. Không sửa đơn đã thu tiền/cấp phát/hủy; thuốc active/giá đã thiết lập."),
            [Policies.AdminOnly] = ("Quản trị hệ thống", "MOD_AUTH", "Chỉ Admin; từng thao tác còn kiểm tra ràng buộc dữ liệu."),
            [Policies.PatientPhoneChange] = ("Đổi SĐT tài khoản cá nhân", "MOD_AUTH", "Chính chủ Patient đã có định danh, đang hoạt động/không chờ kích hoạt; xác nhận mật khẩu và phạm vi, lưu số mới chưa xác minh và thu hồi phiên cùng giao dịch. OTP ở lần đăng nhập kế tiếp; không đồng bộ số trẻ được giám hộ (DL-355/DL-367)."),
            [Policies.StaffAny] = ("Truy cập nghiệp vụ nhân viên", "MOD_AUTH", "Các nghiệp vụ nội bộ; không áp dụng cho tài khoản bệnh nhân."),
            [Policies.AppointmentRequestReview] = ("Tiếp nhận yêu cầu đặt lịch", "MOD_APP", "Admin/Lễ tân xem Pending theo hồ sơ Completed và từ chối có lý do; không giành chỗ hoặc đổi hàng đợi khám."),
            [Policies.PatientManage] = ("Tạo hồ sơ hành chính bệnh nhân", "MOD_PAT", "Thông tin cá nhân do Lễ tân hoặc Admin quản lý."),
            [Policies.PatientEdit] = ("Sửa thông tin hành chính", "MOD_PAT", "Phụ tá không sửa tên, số điện thoại hoặc dữ liệu hành chính."),
            [Policies.PatientView] = ("Xem hồ sơ bệnh nhân", "MOD_PAT", "Bệnh nhân chỉ xem hồ sơ thuộc tài khoản của mình."),
            [Policies.MedicalHistoryWrite] = ("Ghi tiền sử bệnh lý", "MOD_PAT", "Còn kiểm tra lần khám và trạng thái hồ sơ."),
            [Policies.VitalSignsWrite] = ("Ghi sinh hiệu", "MOD_PAT", "Còn kiểm tra lần khám và trạng thái hồ sơ."),
            [Policies.PatientSafetyAlertsView] = ("Xem cảnh báo an toàn", "MOD_PAT", "Dữ liệu nội bộ trong phạm vi công việc của nhân viên."),
            [Policies.WorkspaceManage] = ("Vị trí nha sĩ và phụ tá", "MOD_MST", "Nha sĩ tự chọn phòng điều trị, Phụ tá tự chọn nha sĩ hỗ trợ trong ngày. Admin chỉ tra cứu và gỡ liên kết khi không vướng ca đang khám (DL-379)."),
            [Policies.DiagnosisCatalogView] = ("Xem gợi ý chẩn đoán", "MOD_MST", "Admin quản lý danh mục; nha sĩ chỉ đọc mục đang dùng. Chẩn đoán lâm sàng vẫn nhập tự do."),
            [Policies.MedicineCatalogView] = ("Xem danh mục thuốc", "MOD_MST", "Admin quản lý danh mục và giá; nha sĩ chỉ đọc để kê đơn."),
            [Policies.StockIssueWrite] = ("Xác nhận tiêu hao thực tế theo ca", "MOD_INV", "Admin/Phụ tá xác nhận số nguyên thực tế theo ca hoàn tất; chưa gợi ý BOM; audit và trừ tồn cùng giao dịch (DL-315)."),
            [Policies.StockView] = ("Xem tồn kho vật tư", "MOD_INV", "Admin/Phụ tá xem tồn và bộ chọn nhập kho; Nha sĩ chỉ đọc vật tư đang dùng (DL-314)."),
            [Policies.StockReceiptWrite] = ("Lập và xem phiếu nhập kho", "MOD_INV", "Admin/Phụ tá xác nhận thủ công; cộng tồn và audit cùng giao dịch (DL-311/314)."),
            [Policies.MedicalSupplyCatalogView] = ("Xem danh mục vật tư tiêu hao", "MOD_MST", "Admin quản lý danh mục vật tư tiêu hao; nha sĩ chỉ đọc mục đang dùng (DL-070, DL-103, DL-167)."),
            [Policies.DentalChairView] = ("Xem danh mục phòng điều trị", "MOD_MST", "Admin/Nha sĩ/Phụ tá xem; chỉ Admin khai báo/sửa danh mục phòng điều trị. Phòng bảo trì/ngừng dùng không phục vụ."),
            [Policies.ServiceCatalogView] = ("Xem danh mục dịch vụ", "MOD_MST", "Nha sĩ/Phụ tá được xem; tạo/sửa danh mục và bảng giá chỉ Admin."),
            [Policies.AppointmentCreate] = ("Đặt lịch khám", "MOD_APP", "Bệnh nhân chỉ đặt cho hồ sơ thuộc tài khoản của mình."),
            [Policies.AppointmentView] = ("Xem lịch hẹn", "MOD_APP", "Bệnh nhân chỉ xem lịch hẹn thuộc tài khoản của mình."),
            [Policies.AppointmentManage] = ("Quản lý lịch hẹn tại quầy", "MOD_APP", "Còn kiểm tra trạng thái và thời gian của lịch hẹn. DL-335/355/367: quầy chỉ khởi tạo đề nghị đổi số theo lịch Scheduled; chủ tài khoản tự xác nhận mật khẩu/phạm vi để lưu và OTP khi đăng nhập lại."),
            [Policies.AppointmentReschedule] = ("Đổi lịch hẹn", "MOD_APP", "Còn kiểm tra hồ sơ sở hữu, trạng thái và thời gian được đổi lịch."),
            [Policies.QueueCheckIn] = ("Tiếp đón, check-in và dọn lượt quá hạn", "MOD_CHK", "Lễ tân/Admin; dọn lượt chỉ Visit chưa bắt đầu, quá mốc 23:30 Việt Nam, có xác nhận (DL-260)."),
            [Policies.QueueView] = ("Xem hàng đợi nội bộ", "MOD_CHK", "Màn TV công khai là luồng đọc riêng với tên bệnh nhân đã che."),
            [Policies.QueueSkip] = ("Đánh dấu vắng mặt", "MOD_CHK", "Chỉ lượt Calling chưa khám; Nha sĩ chỉ thao tác lượt của mình. Gọi lại dành cho Lễ tân/Admin (DL-259)."),
            [Policies.QueueCall] = ("Gọi số mời vào phòng", "MOD_CHK", "Phụ tá chỉ gọi lượt đã phân nha sĩ. Lời gọi của Admin và Phụ tá nằm trong hàng đợi nội bộ. Bắt đầu khám là thao tác riêng (DL-258)."),
            [Policies.QueueTvCall] = ("Phát lại lời mời trên TV", "MOD_CHK", "Nha sĩ đang phụ trách gọi lại lượt còn đang mời ở phòng hợp lệ. Lời mời công khai có hiệu lực 15 giây và giữ nguyên lần khám."),
            [Policies.QueueStatusUpdate] = ("Chuyển trạng thái hàng đợi", "MOD_CHK", "Mỗi bước chuyển kiểm tra quyền riêng: Nha sĩ khám/chỉ định chụp; Phụ tá hoàn tất chụp."),
            [Policies.QueueTransfer] = ("Chuyển bệnh nhân sang nha sĩ khác", "MOD_CHK", "Admin/Lễ tân hoặc nha sĩ đang phụ trách; ca InProgress chuyển ghế cùng giao dịch (DL-086/DL-319)."),
            [Policies.ClinicalDiagnosisUpdate] = ("Khám và ghi chẩn đoán", "MOD_PAT", "Chỉ Admin hoặc nha sĩ phụ trách; lần khám đang mở, chưa khóa."),
            [Policies.FdiConditionWrite] = ("Ghi tình trạng răng", "MOD_FDI", "Chỉ Admin hoặc nha sĩ phụ trách; lần khám đang mở, chưa khóa."),
            [Policies.FdiServiceAssign] = ("Chỉ định dịch vụ điều trị", "MOD_FDI", "Chỉ Admin hoặc nha sĩ phụ trách; còn kiểm tra lần khám và dịch vụ đang dùng."),
            [Policies.InvoiceDraftView] = ("Xem hóa đơn theo lần khám", "MOD_BIL", "Bệnh nhân chỉ xem hóa đơn thuộc hồ sơ của mình."),
            [Policies.InvoicePortalRead] = ("Xem hóa đơn của tôi", "MOD_BIL", "Chỉ Patient xem hóa đơn, dòng dịch vụ và khoản thu của đúng hồ sơ duy nhất gắn với tài khoản; kiểm tra ownership ở backend."),
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
