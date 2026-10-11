using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using Dental.Application.Common;
using Dental.Domain.Entities;

namespace Dental.Application.Features.MOD_BIL.Services;

public static class BankTransferRules
{
    private static readonly Regex CodePattern = new(@"(?<![A-Z0-9])PKNK\s+(INV-\d{8}-\d{4})(?![A-Z0-9-])",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant, TimeSpan.FromMilliseconds(100));
    public static string? InvoiceCode(string? content)
    {
        if (string.IsNullOrWhiteSpace(content) || content.Length > 500) return null;
        var matches = CodePattern.Matches(content);
        return matches.Count == 1 ? matches[0].Groups[1].Value.ToUpperInvariant() : null;
    }
    public static Guid RequestId(string source, string reference)
        => new(SHA256.HashData(Encoding.UTF8.GetBytes($"MOD_BIL:{source}:{reference}")).AsSpan(0, 16));
    public static Result Apply(Invoice invoice, decimal received)
    {
        if (received <= 0 || received > InvoiceServiceSnapshot.MaximumAmount || decimal.Truncate(received) != received)
            return Result.Failure(Error.Validation);
        // Q-167: bank amount received is separate; only the outstanding debt is credited.
        return CashPaymentRules.Apply(invoice, Math.Min(received, invoice.TotalAmount - invoice.PaidAmount));
    }
    public static string? ExcessNote(decimal excess) => excess > 0
        ? $"Chuyển dư {excess.ToString("0", CultureInfo.InvariantCulture)} VND, cần hoàn lại cho khách" : null;
}
