using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;

namespace Dental.Application.Features.MOD_BIL.Services;

public static class InvoiceDiscountRules
{
    public static bool ValidValue(string? type, decimal value) => value > 0 && (type switch
    {
        "Percentage" => value <= 100 && decimal.Round(value, 2) == value,
        "FixedAmount" => value <= InvoiceServiceSnapshot.MaximumAmount && decimal.Truncate(value) == value,
        _ => false
    });

    public static Result<decimal> Calculate(Invoice invoice, string type, decimal value)
    {
        if (!ValidValue(type, value)) return Result<decimal>.Failure(Error.Validation);
        var items = invoice.Items;
        if (items.Any(x => x.ItemType is not ("Service" or "Medicine") || x.Quantity <= 0 ||
                x.UnitPrice < 0 || x.TotalAmount < 0 || x.TotalAmount > InvoiceServiceSnapshot.MaximumAmount ||
                x.TotalAmount != x.UnitPrice * x.Quantity) || items.Sum(x => x.TotalAmount) != invoice.TotalAmount)
            return Result<decimal>.Failure(new Error("BIL_024", "Chi tiết hóa đơn đã thay đổi; tải lại trước khi giảm giá."));
        var serviceTotal = items.Where(x => x.ItemType == "Service").Sum(x => x.UnitPrice * x.Quantity);
        // DL-140/DL-154: percentage excludes medicines and rounds only to 1 VND, away from zero.
        var amount = type == "Percentage" ? Math.Round(serviceTotal * value / 100m, MidpointRounding.AwayFromZero) : value;
        return amount <= invoice.TotalAmount ? Result<decimal>.Success(amount) : Result<decimal>.Failure(
            new Error("BIL_025", "Giảm giá không được vượt tổng tiền hóa đơn."));
    }

    public static Result Apply(Invoice invoice, DiscountPreset? preset, ApplyInvoiceDiscountRequest request, string? role)
    {
        if (role is not (RoleCodes.Admin or RoleCodes.Receptionist)) return Result.Failure(Error.Forbidden);
        if (invoice.Status != InvoiceStatus.PendingPayment || invoice.PaidAmount != 0 ||
            invoice.Visit.Status != VisitStatuses.Completed || !invoice.Visit.IsLocked)
            return Result.Failure(new Error("BIL_021", "Chỉ sửa giảm giá trên hóa đơn chờ thanh toán chưa có khoản thu."));
        if (invoice.TotalAmount != request.ExpectedTotalAmount || invoice.DiscountAmount != request.ExpectedCurrentDiscountAmount)
            return Result.Failure(new Error("BIL_006", "Hóa đơn đã thay đổi; tải lại và xác nhận lại giảm giá."));
        if (request.Mode == "Custom" && role != RoleCodes.Admin) return Result.Failure(Error.Forbidden);
        if (request.Mode == "Preset" && (preset is null || !preset.IsActive))
            return Result.Failure(new Error("BIL_022", "Mẫu giảm giá không tồn tại hoặc đã ngừng sử dụng."));
        var type = request.Mode == "Preset" ? preset!.DiscountType : request.DiscountType;
        var value = request.Mode == "Preset" ? preset!.Value : request.Value;
        var calculated = request.Mode == "Remove" ? Result<decimal>.Success(0) : Calculate(invoice, type!, value ?? 0);
        if (calculated.IsFailure) return Result.Failure(calculated.Error);
        var amount = calculated.Value;
        if (amount != request.ExpectedDiscountAmount)
            return Result.Failure(new Error("BIL_006", "Giá trị giảm giá đã thay đổi; tải lại và xác nhận lại."));
        if (role == RoleCodes.Receptionist && (amount > 500000m ||
            type == "Percentage" && value > 25m || amount > invoice.TotalAmount * .25m))
            return Result.Failure(new Error("BIL_023", "Giảm giá vượt 25% hoặc 500.000 VND cần Admin thực hiện kèm lý do."));
        invoice.DiscountAmount = amount;
        invoice.DiscountType = request.Mode == "Remove" ? null : type;
        invoice.DiscountValue = request.Mode == "Remove" ? null : value;
        invoice.DiscountPresetId = request.Mode == "Preset" ? preset!.Id : null;
        invoice.DiscountReason = request.Reason.Trim();
        // DL-135: a confirmed zero payable balance is settled; no zero-value payment is fabricated.
        if (invoice.FinalAmount == 0) invoice.Status = InvoiceStatus.Paid;
        return Result.Success();
    }
}
