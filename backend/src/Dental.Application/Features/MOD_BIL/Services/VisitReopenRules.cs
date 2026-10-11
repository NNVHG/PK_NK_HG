using Dental.Application.Common;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_BIL.Services;

public static class VisitReopenRules
{
    public static Result Validate(Visit visit, Invoice? invoice, bool otherOpenVisit, bool hasPreviousPayment = false)
    {
        if (visit.Status != VisitStatuses.Completed || !visit.IsLocked)
            return Result.Failure(new Error("BIL_008", "Chỉ mở lại lần khám đã hoàn tất và khóa."));
        if (otherOpenVisit)
            return Result.Failure(new Error("BIL_009", "Bệnh nhân đang có lần khám khác chưa kết thúc."));
        if (hasPreviousPayment || invoice is not null && (invoice.PaidAmount != 0 || invoice.Status is not (InvoiceStatus.Draft or InvoiceStatus.PendingPayment)))
            return Result.Failure(new Error("BIL_010", "Không mở khóa hồ sơ có hóa đơn đã thanh toán hoặc không thể hủy."));
        return Result.Success();
    }
}
