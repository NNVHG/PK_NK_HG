using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Dental.Application.Common;
using Dental.Application.Features.MOD_BIL.Services;
using Dental.Application.Features.MOD_MST.Services;
using Dental.Application.Features.MOD_RX.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
namespace Dental.Application.Features.MOD_RX.Services;
public static class PrescriptionRules
{
    public static bool CanEdit(Visit visit, Invoice? invoice, bool hasHistory, Prescription? rx)
    {
        if (hasHistory || rx?.IsCancelled == true || rx?.DispensedAt is not null || (invoice is not null && invoice.PaidAmount != 0) ||
            invoice?.Status is InvoiceStatus.Paid or InvoiceStatus.PartiallyPaid) return false;
        if (visit.Status is VisitStatuses.InProgress or VisitStatuses.Correcting)
            return !visit.IsLocked && (invoice is null || invoice.Status == InvoiceStatus.Draft);
        return visit.Status == VisitStatuses.Completed && visit.IsLocked && invoice?.Status == InvoiceStatus.PendingPayment && rx is not null;
    }
    public static Result<PrescriptionPlan> Build(PrescriptionSnapshot state, PrescriptionWriteRequest request, bool enforceConfirmations)
    {
        if (!CanEdit(state.Visit, state.Invoice, state.HasReceiptHistory, state.Prescription)) return Fail("RX_002", "Đơn đã khóa hoặc ca khám chưa cho phép kê đơn.");
        if ((state.Prescription?.Version ?? 0) != request.ExpectedVersion) return Fail("RX_003", "Đơn vừa thay đổi; tải lại trước khi lưu.");
        var medicines = state.Medicines.ToDictionary(m => m.Id);
        var existing = state.Prescription?.Items.ToDictionary(i => i.MedicineId) ?? new Dictionary<int, PrescriptionItem>();
        var lines = new List<PrescriptionItem>(); var warnings = new List<PrescriptionWarning>();
        decimal medicineTotal = 0;
        foreach (var input in request.Items)
        {
            if (!medicines.TryGetValue(input.MedicineId, out var medicine) || !medicine.IsActive || medicine.UnitPrice <= 0)
                return Fail("RX_004", "Có thuốc đã ngừng dùng hoặc chưa thiết lập giá; tải lại và chọn thuốc hoạt động.");
            if (medicine.Version != input.MedicineVersion) return Fail("RX_003", "Danh mục thuốc vừa thay đổi; tải lại để kiểm tra sản phẩm và giá.");
            existing.TryGetValue(input.MedicineId, out var old);
            var line = new PrescriptionItem { Id = old?.Id ?? 0, MedicineId = medicine.Id,
                MedicineCode = old?.MedicineCode ?? medicine.Code, MedicineName = old?.MedicineName ?? medicine.Name,
                ActiveIngredient = old is null ? medicine.ActiveIngredient : old.ActiveIngredient, Strength = old is null ? medicine.Strength : old.Strength,
                Unit = old?.Unit ?? medicine.Unit, UnitPrice = old?.UnitPrice ?? medicine.UnitPrice,
                Quantity = input.Quantity, UsageInstructions = input.UsageInstructions.Trim(), TreatmentDays = input.TreatmentDays };
            if (line.UnitPrice < 0 || decimal.Truncate(line.UnitPrice) != line.UnitPrice ||
                line.UnitPrice > (InvoiceServiceSnapshot.MaximumAmount - medicineTotal) / input.Quantity)
                return Fail("RX_005", "Tổng tiền đơn thuốc vượt giới hạn lưu trữ.");
            medicineTotal += line.TotalPrice;
            var matches = MatchingAllergies(line.MedicineName, line.ActiveIngredient, state.Allergies).ToArray();
            if (matches.Length > 0)
            {
                warnings.Add(new(line.MedicineId, matches.Select(a => a.ItemId).ToArray(), matches.Select(a => a.Name).ToArray()));
                if (enforceConfirmations && (!input.AllergyConfirmed || string.IsNullOrWhiteSpace(input.AllergyOverrideReason) || input.AllergyOverrideReason.Trim().Length is < 5 or > 500))
                    return Fail("RX_006", "Xác nhận dị ứng và nhập lý do chuyên môn từ 5 đến 500 ký tự cho từng thuốc có cảnh báo.");
                line.AllergyOverrideReason = string.IsNullOrWhiteSpace(input.AllergyOverrideReason) ? null : input.AllergyOverrideReason.Trim();
            }
            lines.Add(line);
        }
        var duplicates = lines.SelectMany(l => Ingredients(l.ActiveIngredient).Select(i => (Ingredient: i, l.MedicineId)))
            .Distinct().GroupBy(x => x.Ingredient).Where(g => g.Select(x => x.MedicineId).Distinct().Count() > 1).Select(g => g.Key).Order().ToArray();
        if (enforceConfirmations && duplicates.Length > 0 && !request.DuplicateIngredientConfirmed)
            return Fail("RX_007", "Xác nhận đã kiểm tra các thuốc trùng hoạt chất trước khi lưu.");
        var snapshot = InvoiceServiceSnapshot.Create(state.Services);
        if (snapshot.IsFailure) return Result<PrescriptionPlan>.Failure(snapshot.Error);
        var servicesTotal = snapshot.Value!.Sum(x => x.TotalAmount);
        if (servicesTotal > InvoiceServiceSnapshot.MaximumAmount - medicineTotal) return Fail("RX_005", "Tổng hóa đơn vượt giới hạn lưu trữ.");
        var discount = state.Invoice?.DiscountAmount ?? 0;
        if (discount > servicesTotal + medicineTotal) return Fail("RX_008", "Giảm giá hiện tại vượt tổng tiền mới; Thu ngân/Admin cần kiểm tra lại giảm giá trước khi đổi đơn.");
        var token = SnapshotToken(state, request);
        if (enforceConfirmations && !string.Equals(token, request.PreviewToken, StringComparison.Ordinal))
            return Fail("RX_003", "Dữ liệu kê đơn hoặc viện phí vừa thay đổi; kiểm tra lại và xác nhận trước khi lưu.");
        return Result<PrescriptionPlan>.Success(new(lines, new(token, warnings, duplicates, medicineTotal, servicesTotal, discount, servicesTotal + medicineTotal - discount)));
    }
    public static IEnumerable<string> Ingredients(string? text) => (text ?? "").Split(['/', '+', ',', ';'], StringSplitOptions.RemoveEmptyEntries)
        .Select(DiagnosisSearch.Normalize).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal);
    public static IEnumerable<MedicalHistoryItem> MatchingAllergies(string name, string? ingredient, IEnumerable<MedicalHistoryItem> history)
    {
        var keys = Ingredients(ingredient).Append(DiagnosisSearch.Normalize(name)).Where(x => x.Length > 0).ToArray();
        return history.Where(a => a.Type == MedicalHistoryTypes.Allergy && !string.IsNullOrWhiteSpace(a.Name))
            .Where(a => { var term = DiagnosisSearch.Normalize(a.Name); return term.Length > 0 && keys.Any(k => k.Contains(term, StringComparison.Ordinal) || term.Contains(k, StringComparison.Ordinal)); });
    }
    private static string SnapshotToken(PrescriptionSnapshot s, PrescriptionWriteRequest r)
    {
        var body = new { s.Visit.VisitId, s.Visit.Status, s.Visit.IsLocked, s.Visit.DentistId,
            invoice = s.Invoice is null ? null : new { s.Invoice.Id, s.Invoice.Status, s.Invoice.TotalAmount, s.Invoice.PaidAmount, s.Invoice.DiscountAmount, s.Invoice.DiscountType, s.Invoice.DiscountValue },
            prescription = new { Id = s.Prescription?.Id, Version = s.Prescription?.Version }, s.AllergyRecordId,
            allergies = s.Allergies.OrderBy(x => x.ItemId).Select(x => new { x.ItemId, x.Name, x.Detail }),
            medicines = s.Medicines.OrderBy(x => x.Id).Select(x => new { x.Id, x.Version, x.Code, x.Name, x.ActiveIngredient, x.Strength, x.Unit, x.UnitPrice, x.IsActive }),
            services = s.Services.OrderBy(x => x.Id).Select(x => new { x.Id, x.ServiceCode, x.ServiceName, x.Quantity, x.UnitPrice, x.ToothNumber, x.Surface }),
            r.Diagnosis, r.DoctorAdvice, r.RevisitDate, r.ExpectedVersion,
            items = r.Items.OrderBy(x => x.MedicineId).Select(x => new { x.MedicineId, x.MedicineVersion, x.Quantity, x.UsageInstructions, x.TreatmentDays }) };
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(body))));
    }
    private static Result<PrescriptionPlan> Fail(string code, string message) => Result<PrescriptionPlan>.Failure(code, message);
}
