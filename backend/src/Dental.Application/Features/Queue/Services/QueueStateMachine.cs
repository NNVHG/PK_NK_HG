using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Enums;

namespace Dental.Application.Features.Queue.Services;

/// <summary>
/// Máy trạng thái hàng đợi theo DL-047, DL-053.
/// 5 trạng thái: Waiting(1), InConsultation(2), Completed(3), Cancelled(4), InImaging(5).
/// </summary>
public static class QueueStateMachine
{
    public static Result ValidateTransition(QueueStatus currentStatus, QueueStatus targetStatus, string roleCode)
    {
        // 1. Không cho phép chuyển từ trạng thái kết thúc (DL-047)
        if (currentStatus == QueueStatus.Completed)
            return Result.Failure(Error.QueueCannotRevertCompleted);

        if (currentStatus == QueueStatus.Cancelled)
            return Result.Failure(Error.QueueCannotRevertCancelled);

        if (currentStatus == targetStatus)
            return Result.Failure(Error.Validation.Code, "Trạng thái mới phải khác trạng thái hiện tại.");

        // 2. Kiểm tra các bước chuyển hợp lệ theo quy trình
        return (currentStatus, targetStatus) switch
        {
            // Chờ khám (1) -> Đang khám (2): Nha sĩ, Admin (DL-047, DL-013)
            (QueueStatus.Waiting, QueueStatus.InConsultation) =>
                roleCode is RoleCodes.Dentist or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Chỉ Nha sĩ hoặc Quản trị viên mới có quyền bắt đầu phiên khám."),

            // Chờ khám (1) -> Đã hủy (4): Lễ tân, Admin (DL-047)
            (QueueStatus.Waiting, QueueStatus.Cancelled) =>
                roleCode is RoleCodes.Receptionist or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Chỉ Lễ tân hoặc Quản trị viên mới có quyền hủy lượt chờ khám."),

            // Đang khám (2) -> Khám xong (3): Nha sĩ, Admin (DL-047)
            (QueueStatus.InConsultation, QueueStatus.Completed) =>
                roleCode is RoleCodes.Dentist or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Chỉ Nha sĩ hoặc Quản trị viên mới có quyền hoàn tất phiên khám."),

            // Đang khám (2) -> Đang chụp ảnh (5): Nha sĩ, Admin (DL-047)
            (QueueStatus.InConsultation, QueueStatus.InImaging) =>
                roleCode is RoleCodes.Dentist or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Chỉ Nha sĩ hoặc Quản trị viên mới có quyền chỉ định chụp X-quang."),

            // Đang khám (2) -> Đã hủy (4): Nha sĩ, Admin (DL-047)
            (QueueStatus.InConsultation, QueueStatus.Cancelled) =>
                roleCode is RoleCodes.Dentist or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Chỉ Nha sĩ hoặc Quản trị viên mới có quyền hủy phiên khám đang diễn ra."),

            // Đang chụp ảnh (5) -> Chờ khám (1): Phụ tá, Admin (DL-053)
            (QueueStatus.InImaging, QueueStatus.Waiting) =>
                roleCode is RoleCodes.Assistant or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Chỉ Phụ tá hoặc Quản trị viên mới có quyền hoàn tất chụp ảnh và chuyển về chờ khám."),

            // Đang chụp ảnh (5) -> Đã hủy (4): Lễ tân, Phụ tá, Admin
            (QueueStatus.InImaging, QueueStatus.Cancelled) =>
                roleCode is RoleCodes.Receptionist or RoleCodes.Assistant or RoleCodes.Admin
                    ? Result.Success()
                    : Result.Failure(Error.QueueTransitionForbidden.Code, "Bạn không có quyền hủy lượt chụp ảnh này."),

            // Mọi chuyển trạng thái khác đều không hợp lệ (ví dụ: Waiting -> Completed)
            _ => Result.Failure(
                Error.QueueInvalidStateTransition.Code,
                $"Không được phép chuyển từ '{QueueStatusDescriptions.GetDescription(currentStatus)}' sang '{QueueStatusDescriptions.GetDescription(targetStatus)}'.")
        };
    }
}
