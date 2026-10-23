using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class DevelopmentDatabaseSeederSmoke
{
    public static async Task RunAsync(DbContextOptions<DentalDbContext> options, Action<bool, string> check)
    {
        await using var db = new DentalDbContext(options);
        var accounts = new (string Phone, string RoleCode)[]
        {
            ("0999999001", RoleCodes.Admin),
            ("0900000002", RoleCodes.Receptionist),
            ("0900000003", RoleCodes.Dentist),
            ("0900000004", RoleCodes.Assistant),
            ("0900000005", RoleCodes.Patient),
        };

        foreach (var (phone, roleCode) in accounts)
        {
            var user = await db.Users.Include(x => x.Role).SingleOrDefaultAsync(x => x.Phone == phone);
            check(user is { IsActive: true } && user.Role.RoleCode == roleCode,
                $"Development seeder tạo tài khoản hoạt động đúng vai trò {roleCode}");
        }

        var prescription = await db.Prescriptions.SingleOrDefaultAsync(x => x.PrescriptionCode.EndsWith("-0001"));
        check(prescription is { DispensedAt: not null, DispensedBy: not null, InvoiceId: not null },
            "Đơn thuốc mẫu được bàn giao và liên kết hóa đơn sau khi seeder hoàn tất");

        var invoice = await db.Invoices.SingleAsync(x => x.Id == prescription!.InvoiceId);
        check(invoice.Status == InvoiceStatus.Paid && invoice.PaidAmount == 585000m,
            "Hóa đơn mẫu vẫn giữ trạng thái và số tiền đã thanh toán");

        var paymentCount = await db.PaymentTransactions.CountAsync(x => x.InvoiceId == invoice.Id && x.Amount == invoice.PaidAmount);
        check(paymentCount == 1, "Hóa đơn mẫu có đúng một giao dịch thu khớp số đã thanh toán");

        var dispenseEvent = await db.PrescriptionDispenseEvents.SingleOrDefaultAsync(x =>
            x.PrescriptionId == prescription!.Id && x.Action == "Dispense");
        check(dispenseEvent is not null && dispenseEvent.PrescriptionVersion == prescription!.Version,
            "Lịch sử bàn giao khớp phiên bản cuối của đơn thuốc mẫu");
    }
}
