using System.Globalization;
using System.Text;
using Dental.Application.Features.MOD_RPT.DTOs;

namespace Dental.Application.Features.MOD_RPT.Services;

public static class RevenueReportCsv
{
    public static byte[] Create(RevenueReportDto report)
    {
        var csv = new StringBuilder();
        void Row(params object[] cells) => csv.AppendJoin(',', cells.Select(cell => Escape(cell is decimal money && money == decimal.Truncate(money)
            ? money.ToString("0", CultureInfo.InvariantCulture) : Convert.ToString(cell, CultureInfo.InvariantCulture) ?? ""))).Append("\r\n");
        var s = report.Summary;
        Row("BÁO CÁO DOANH THU", report.Filter.FromDate, report.Filter.ToDate);
        Row("Thời điểm cập nhật UTC+7", report.GeneratedAt.AddHours(7).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture));
        Row("Bộ lọc thu ngân", report.Filter.CashierId?.ToString(CultureInfo.InvariantCulture) ?? "Tất cả");
        Row("Thực thu trước hoàn", s.GrossRevenue); Row("Đã hoàn trong kỳ", s.TotalRefunded); Row("Doanh thu thuần trong kỳ", s.NetRevenue);
        Row("Giá trị sau giảm của hóa đơn xác nhận lập trong kỳ (toàn phòng khám)", s.TotalInvoiced);
        Row("Chiết khấu hóa đơn xác nhận lập trong kỳ (toàn phòng khám)", s.TotalDiscount);
        Row("Tổng nợ hiện tại toàn phòng khám (không theo kỳ/thu ngân)", s.TotalOutstanding);
        Row("Khoản thu trong kỳ của hóa đơn hiện PartiallyPaid", s.TemporaryCollections);
        Row("Hóa đơn lập trong kỳ hiện đã hủy trước thu tiền", s.CancelledInvoiceCount);
        Row("Theo ngày UTC+7", "Doanh thu thuần", "Số giao dịch", "Thu trước hoàn", "Đã hoàn");
        foreach (var d in report.DailyRevenue) Row(d.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), d.Revenue, d.TransactionCount, d.GrossRevenue, d.TotalRefunded);
        Row("Mốc thời gian " + report.Filter.GroupBy, "Doanh thu thuần", "Số giao dịch");
        foreach (var d in report.RevenueTimeline) Row(d.Label, d.Revenue, d.TransactionCount);
        Row("Phương thức", "Thuần", "Thu trước hoàn", "Đã hoàn", "% tiền thu trước hoàn");
        foreach (var m in report.ByPaymentMethod) Row(m.Method, m.Amount, m.GrossRevenue, m.TotalRefunded, m.Percentage);
        Row("Mã thu ngân", "Tên", "Thuần", "Số giao dịch");
        foreach (var c in report.ByCashier) Row(c.CashierId, c.Name, c.Amount, c.TransactionCount);
        Row("Top dịch vụ: hóa đơn hiện Paid; cơ cấu kỳ cũ cập nhật sau tất toán");
        Row("Mã dịch vụ", "Dịch vụ", "Số lượng thực hiện", "Thực thu phân bổ sau chiết khấu");
        foreach (var t in report.TopServices) Row(t.ServiceCode, t.ServiceName, t.UsageCount, t.Revenue);
        return Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
    }
    public static string FileName(RevenueReportDto report) => $"BaoCao_DoanhThu_{report.Filter.FromDate:yyyyMMdd}_{report.Filter.ToDate:yyyyMMdd}_{report.GeneratedAt.AddHours(7):HHmmss}.csv";
    private static string Escape(string value)
    {
        // Treat user-controlled staff/service labels as text, including whitespace-prefixed Excel formulas.
        var trimmed = value.TrimStart();
        if (trimmed.Length > 0 && "=+@-".Contains(trimmed[0]) && !decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out _)) value = "'" + value;
        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }
}
