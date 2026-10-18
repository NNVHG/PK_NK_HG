using System.Net;
using System.Net.Http.Json;
using System.Text;
using Dental.Application.Features.MOD_RPT.DTOs;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Dental.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

internal static class RevenueReportHttpSmoke
{
    public static async Task RunAsync(HttpClient client, DbContextOptions<DentalDbContext> options, int actorId, int patientId,
        Action<string> authenticate, Action<bool, string> check)
    {
        const string path = "/api/reports/revenue";
        const string jan = "?fromDate=2025-01-01&toDate=2025-01-31&top=All";
        const string feb = "?fromDate=2025-02-01&toDate=2025-02-28&top=All";
        client.DefaultRequestHeaders.Authorization = null;
        foreach (var endpoint in new[] { path, path + "/export" })
        { using var response = await client.GetAsync(endpoint + jan); check(response.StatusCode == HttpStatusCode.Unauthorized, "Report and export anonymous401"); }
        foreach (var role in new[] { RoleCodes.Receptionist, RoleCodes.Dentist, RoleCodes.Assistant, RoleCodes.Patient })
        {
            authenticate(role);
            foreach (var endpoint in new[] { path, path + "/export" })
            { using var response = await client.GetAsync(endpoint + jan); check(response.StatusCode == HttpStatusCode.Forbidden, role + " cannot access report/export403"); }
        }
        authenticate(RoleCodes.Admin);
        async Task<RevenueReportDto> Get(string query)
        {
            using var response = await client.GetAsync(path + query);
            if (response.StatusCode != HttpStatusCode.OK) throw new Exception("Report HTTP failure: " + response.StatusCode + " " + await response.Content.ReadAsStringAsync());
            check(response.Headers.CacheControl?.NoStore == true, "Private report NoStore");
            return (await response.Content.ReadFromJsonAsync<RevenueReportDto>())!;
        }
        var empty = await Get(jan);
        check(empty.Summary.NetRevenue == 0 && empty.DailyRevenue.Count == 31 && empty.DailyRevenue.All(d => d.Revenue == 0) && empty.TopServices.Count == 0, "Empty period has honest zero cash and no synthetic services");
        var baseDebt = empty.Summary.TotalOutstanding;
        await using var db = new DentalDbContext(options);
        var actor = await db.Users.SingleAsync(u => u.UserId == actorId);
        var other = new User { FullName = "Synthetic second cashier", Phone = "0990000113", PasswordHash = "test-only", RoleId = actor.RoleId };
        db.Users.Add(other); await db.SaveChangesAsync();
        DateTime Utc(int month, int day = 5) => new(2025, month, day, 0, 0, 0, DateTimeKind.Utc);
        var serial = 0;
        Invoice Invoice(decimal total, InvoiceStatus status, DateTime created, decimal paid = 0, decimal discount = 0, string? discountType = null, bool medicine = false)
        {
            serial++;
            var invoice = new Invoice { InvoiceCode = "REPORT-" + serial, TotalAmount = total, PaidAmount = paid, DiscountAmount = discount, DiscountType = discountType,
                CreatedAt = created, CreatedByUserId = actorId, Status = status,
                Visit = new Visit { PatientId = patientId, CreatedByUserId = actorId, Status = VisitStatuses.Completed, IsLocked = true },
                Items = [new InvoiceItem { Code = "RPT-S" + serial, Name = "Synthetic service " + serial, Quantity = 1, UnitPrice = medicine ? total - 100 : total, TotalAmount = medicine ? total - 100 : total }] };
            if (medicine) invoice.Items.Add(new InvoiceItem { ItemType = "Medicine", Code = "RPT-M", Name = "Synthetic medicine", Quantity = 1, UnitPrice = 100, TotalAmount = 100 });
            db.Invoices.Add(invoice); return invoice;
        }
        void Receipt(Invoice invoice, DateTime at, decimal amount, int? cashier = null, string method = "Cash") => db.PaymentTransactions.Add(new PaymentTransaction
        { Invoice = invoice, PaidAt = at, Amount = amount, CashierId = cashier ?? actorId, PaymentMethod = method, RequestId = Guid.NewGuid() });
        var settled = Invoice(1000, InvoiceStatus.Paid, Utc(1), 1000); Receipt(settled, Utc(1), 400); Receipt(settled, Utc(2), 600, other.UserId, "BankTransfer");
        var partial = Invoice(1000, InvoiceStatus.PartiallyPaid, Utc(1), 400); Receipt(partial, Utc(1), 400);
        var pending = Invoice(1000, InvoiceStatus.PendingPayment, Utc(2), discount: 100);
        Invoice(9000, InvoiceStatus.Draft, Utc(2)); Invoice(9000, InvoiceStatus.Cancelled, Utc(2));
        var mixed = Invoice(1000, InvoiceStatus.Paid, Utc(1), 910, 90, "Percentage", true); Receipt(mixed, Utc(1), 364); Receipt(mixed, Utc(2), 546);
        var refunded = Invoice(100, InvoiceStatus.Cancelled, Utc(1)); Receipt(refunded, Utc(1), 100); Receipt(refunded, Utc(2), -100);
        var boundary = Invoice(4, InvoiceStatus.Paid, Utc(1), 4);
        var start = new DateTime(2025, 1, 31, 17, 0, 0, DateTimeKind.Utc); var until = new DateTime(2025, 2, 28, 17, 0, 0, DateTimeKind.Utc);
        Receipt(boundary, start.AddTicks(-10), 1); Receipt(boundary, start, 1); Receipt(boundary, until.AddTicks(-10), 1); Receipt(boundary, until, 1);
        var free = Invoice(100, InvoiceStatus.Paid, Utc(2), 0, 100, "Percentage");
        await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var a = await Get(jan); var b = await Get(feb);
        check(a.Summary.GrossRevenue == 1265 && a.Summary.TotalRefunded == 0 && a.Summary.NetRevenue == 1265, "January uses receipts including partial collections and historical refunded-invoice cash");
        check(b.Summary.GrossRevenue == 1148 && b.Summary.TotalRefunded == 100 && b.Summary.NetRevenue == 1048, "February gross/refund/net use actual movement dates");
        check(a.Summary.TotalOutstanding == baseDebt + 1500 && b.Summary.TotalOutstanding == a.Summary.TotalOutstanding, "Current all-clinic debt independent of report period; Draft/Cancelled/Paid excluded");
        check(b.Summary.TotalInvoiced == 900 && b.Summary.TotalDiscount == 200 && b.Summary.CancelledInvoiceCount == 1, "Creation-period confirmed invoice totals exclude Draft/Cancelled; free Paid has no fake cash");
        check(a.Summary.TemporaryCollections == 400 && b.Summary.TemporaryCollections == 0, "PartiallyPaid receipts included in total and identified as temporary");
        check(a.TopServices.Single(s => s.ServiceCode == settled.Items.Single().Code).Revenue == 400 && b.TopServices.Single(s => s.ServiceCode == settled.Items.Single().Code).Revenue == 600, "Settled installments allocate to original payment periods Q311a");
        check(a.TopServices.All(s => s.ServiceCode != partial.Items.Single().Code), "PartiallyPaid never allocated to services");
        check(a.TopServices.Single(s => s.ServiceCode == mixed.Items.First().Code).Revenue == 324 && b.TopServices.Single(s => s.ServiceCode == mixed.Items.First().Code).Revenue == 486, "Percentage discount excludes medicine and preserves service cash allocation");
        check(b.TopServices.All(s => !s.ServiceCode.StartsWith("RPT-M") && s.ServiceCode != refunded.Items.Single().Code && s.ServiceCode != free.Items.Single().Code), "Medicine, refunded services and free Paid do not invent service cash");
        check(b.DailyRevenue.Single(d => d.Date == new DateOnly(2025, 2, 1)).Revenue == 1 && b.DailyRevenue.Single(d => d.Date == new DateOnly(2025, 2, 28)).Revenue == 1, "UTC+7 boundaries include last PostgreSQL microsecond and exclude next midnight");
        check(b.DailyRevenue.Sum(d => d.Revenue) == b.Summary.NetRevenue && b.RevenueTimeline.Sum(t => t.Revenue) == b.Summary.NetRevenue && b.ByPaymentMethod.Sum(m => m.Amount) == b.Summary.NetRevenue && b.ByCashier.Sum(c => c.Amount) == b.Summary.NetRevenue, "Timeline, methods and cashiers reconcile with cash summary");
        check(b.ByPaymentMethod.Sum(m => m.Percentage) == 100 && b.ByPaymentMethod.Single(m => m.Method == "BankTransfer").Amount == 600, "Method percentages reconcile and bank transfer reported separately");
        var filtered = await Get(feb + "&cashierId=" + other.UserId);
        check(filtered.Summary.NetRevenue == 600 && filtered.Summary.TotalOutstanding == b.Summary.TotalOutstanding && filtered.Summary.TotalInvoiced == b.Summary.TotalInvoiced, "Cashier filter affects cash only; clinic debt/issued totals remain explicit whole-clinic metrics");
        check(filtered.TopServices.Count == 1 && filtered.TopServices[0].Revenue == 600, "Cashier filter does not reallocate prior payments");
        var week = await Get("?fromDate=2024-12-30&toDate=2025-01-01&groupBy=Week"); check(week.RevenueTimeline.Single().Label == "2025-W01", "ISO week uses week-year across New Year");
        var auto = await Get("?fromDate=2025-01-01&toDate=2025-04-01"); check(auto.Filter.GroupBy == "Month", "Auto month grouping after90days");
        var defaultReport = await Get(""); check(defaultReport.Filter.FromDate.Day == 1 && defaultReport.Filter.ToDate.Day == DateTime.DaysInMonth(defaultReport.Filter.FromDate.Year, defaultReport.Filter.FromDate.Month), "Missing dates default to full current Vietnam month");
        foreach (var bad in new[] { "?fromDate=2025-02-01&toDate=2025-01-01", "?fromDate=2025-01-01&toDate=2026-01-04", "?fromDate=invalid", "?top=100", "?groupBy=Year", "?sortBy=Patient", "?cashierId=-1", "?fromDate=0001-01-01&toDate=0001-01-01" })
        { using var response = await client.GetAsync(path + bad); check(response.StatusCode == HttpStatusCode.BadRequest, "Invalid report filter400 " + bad); }
        using (var allowed = await client.GetAsync(path + "?fromDate=2024-01-01&toDate=2025-01-01")) check(allowed.StatusCode == HttpStatusCode.OK, "Approved366daydifference is accepted");
        var beforePayments = await db.PaymentTransactions.CountAsync(); var beforeAudit = await db.AuditLogs.CountAsync();
        using (var export = await client.GetAsync(path + "/export" + feb))
        {
            var bytes = await export.Content.ReadAsByteArrayAsync(); var text = Encoding.UTF8.GetString(bytes);
            check(export.StatusCode == HttpStatusCode.OK && export.Content.Headers.ContentType?.MediaType == "text/csv" && bytes.Take(3).SequenceEqual(new byte[] { 239, 187, 191 }), "CSV realHTTP UTF8BOM/native content type");
            check(export.Headers.CacheControl?.NoStore == true && export.Content.Headers.ContentDisposition?.FileName?.Contains("BaoCao_DoanhThu_20250201_20250228_") == true, "CSV has private cache and safe timestamped filename");
            check(text.Contains("Tổng nợ hiện tại toàn phòng khám") && text.Contains("\"1048\"") && !text.Contains("Synthetic patient") && !text.Contains("0999999992"), "CSV contains correct net/debt scope and no patient information");
        }
        check(beforePayments == await db.PaymentTransactions.CountAsync() && beforeAudit == await db.AuditLogs.CountAsync(), "Reading/export never alters payments, invoices or audit");
        // Close the existing partial invoice: cash totals stay identical, historical service structure becomes available.
        var tracked = await db.Invoices.FindAsync(partial.Id); tracked!.PaidAmount = 1000; tracked.Status = InvoiceStatus.Paid;
        Receipt(tracked, Utc(2), 600); await db.SaveChangesAsync(); db.ChangeTracker.Clear();
        var after = await Get(jan);
        check(after.Summary.NetRevenue == a.Summary.NetRevenue && after.TopServices.Single(s => s.ServiceCode == partial.Items.Single().Code).Revenue == 400 && after.Summary.TemporaryCollections == 0, "Settlement updates prior service structure without rewriting historical cash totals");
        check(after.Summary.TotalOutstanding == a.Summary.TotalOutstanding - 600, "Current debt decreases after settlement even when viewing January");
    }
}
