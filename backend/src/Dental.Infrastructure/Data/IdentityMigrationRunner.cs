using System.Data;
using System.Text.Json;
using System.Text.RegularExpressions;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Entities;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>
/// Chuyển đổi an toàn dữ liệu định danh cũ (SHA-256) sang HMAC khóa phiên bản hiện hành.
/// Có guard từ chối database production `dental_db`, hỗ trợ dry-run và idempotent.
/// </summary>
public static class IdentityMigrationRunner
{
    private static readonly Regex CitizenIdPattern = new(@"^\d{9}(\d{3})?$", RegexOptions.Compiled);
    private static readonly Regex PassportPattern = new(IdentityDocumentConstants.PassportNumberRegex, RegexOptions.Compiled);
    private static readonly Regex CountryPattern = new(IdentityDocumentConstants.NationalityCodeRegex, RegexOptions.Compiled);

    /// <summary>
    /// Kiểm tra guard từ chối cơ sở dữ liệu production `dental_db`.
    /// </summary>
    public static void AssertNotProductionDatabase(DentalDbContext db)
    {
        var dbName = db.Database.GetDbConnection().Database;
        if (string.Equals(dbName, "dental_db", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("Guard: Thao tác kiểm kê/chuyển đổi danh tính trực tiếp trên database production 'dental_db' bị từ chối.");
        }
    }

    /// <summary>
    /// Kiểm kê an toàn chỉ đọc, có guard chặn database production.
    /// </summary>
    public static async Task<IdentityInventory> InspectSafeAsync(DentalDbContext db, CancellationToken ct = default)
    {
        AssertNotProductionDatabase(db);
        return await IdentityMigrationPreflight.InspectAsync(db, ct);
    }

    /// <summary>
    /// Bản ghi đối soát xác minh một hồ sơ bệnh nhân từ kênh hợp lệ.
    /// </summary>
    public sealed record VerifiedIdentityItem(
        int PatientId,
        string DocumentNumber,
        IdentityDocumentType DocumentType,
        string? IssuingCountryCode);

    /// <summary>
    /// Kết quả thực thi chuyển đổi danh tính.
    /// </summary>
    public sealed record IdentityConversionResult(
        int TotalProcessed,
        int ConvertedCount,
        int AlreadyUpToDateCount,
        bool IsDryRun);

    public static async Task<IdentityConversionResult> ConvertVerifiedBatchAsync(
        DentalDbContext db,
        IKeyedIdentityHasher hasher,
        int actorUserId,
        IReadOnlyList<VerifiedIdentityItem> items,
        bool isDryRun = false,
        CancellationToken ct = default)
    {
        AssertNotProductionDatabase(db);
        ArgumentNullException.ThrowIfNull(hasher);
        ArgumentNullException.ThrowIfNull(items);

        if (items.Count == 0)
            return new IdentityConversionResult(0, 0, 0, isDryRun);

        ValidateItems(items);

        var targetKeyVersion = hasher.CurrentVersion;
        if (targetKeyVersion <= 0)
            throw new InvalidOperationException("CurrentVersion của hasher phải là số nguyên dương.");

        if (isDryRun)
            return await ExecuteDryRunAsync(db, hasher, targetKeyVersion, items, ct);

        return await ExecuteConversionAsync(db, hasher, actorUserId, targetKeyVersion, items, ct);
    }
    private static void ValidateItems(IReadOnlyList<VerifiedIdentityItem> items)
    {
        foreach (var item in items)
        {
            if (item.PatientId <= 0)
                throw new ArgumentException($"PatientId không hợp lệ: {item.PatientId}");

            var rawNumber = item.DocumentNumber?.Trim() ?? string.Empty;
            if (item.DocumentType == IdentityDocumentType.CitizenId)
            {
                if (!CitizenIdPattern.IsMatch(rawNumber) || !string.IsNullOrWhiteSpace(item.IssuingCountryCode))
                    throw new ArgumentException("Số CCCD hoặc mã quốc gia không hợp lệ.");
            }
            else if (item.DocumentType == IdentityDocumentType.Passport)
            {
                if (!PassportPattern.IsMatch(rawNumber) || string.IsNullOrWhiteSpace(item.IssuingCountryCode) ||
                    !CountryPattern.IsMatch(item.IssuingCountryCode.Trim()))
                    throw new ArgumentException("Số hộ chiếu hoặc quốc gia phát hành không hợp lệ.");
            }
            else
            {
                throw new ArgumentException($"Loại giấy tờ không hỗ trợ: {item.DocumentType}");
            }
        }
    }

    private static async Task<IdentityConversionResult> ExecuteDryRunAsync(
        DentalDbContext db,
        IKeyedIdentityHasher hasher,
        int targetKeyVersion,
        IReadOnlyList<VerifiedIdentityItem> items,
        CancellationToken ct)
    {
        var patientIds = items.Select(x => x.PatientId).Distinct().ToList();
        var existingPatients = await db.Patients.AsNoTracking()
            .Where(p => patientIds.Contains(p.PatientId))
            .ToDictionaryAsync(p => p.PatientId, ct);

        var dryRunConverted = 0;
        var dryRunUpToDate = 0;

        foreach (var item in items)
        {
            if (!existingPatients.TryGetValue(item.PatientId, out var patient))
                throw new InvalidOperationException($"Không tìm thấy PatientId: {item.PatientId}");

            var canonicalDoc = item.DocumentNumber.Trim();
            var expectedHmac = hasher.Hash(canonicalDoc, item.DocumentType, item.IssuingCountryCode, targetKeyVersion);

            var isUpToDate = patient.CitizenIdHash == expectedHmac &&
                             patient.DocumentType == item.DocumentType &&
                             patient.HashScheme == IdentityHashScheme.HmacCanonical &&
                             patient.IdentityKeyVersion == targetKeyVersion;

            if (isUpToDate)
                dryRunUpToDate++;
            else
                dryRunConverted++;
        }

        return new IdentityConversionResult(items.Count, dryRunConverted, dryRunUpToDate, IsDryRun: true);
    }
    private static async Task<IdentityConversionResult> ExecuteConversionAsync(
        DentalDbContext db,
        IKeyedIdentityHasher hasher,
        int actorUserId,
        int targetKeyVersion,
        IReadOnlyList<VerifiedIdentityItem> items,
        CancellationToken ct)
    {
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, ct);

        var pIds = items.Select(x => x.PatientId).Distinct().ToList();
        var patientsToUpdate = await db.Patients
            .Where(p => pIds.Contains(p.PatientId))
            .ToDictionaryAsync(p => p.PatientId, ct);

        var converted = 0;
        var alreadyUpToDate = 0;
        var now = DateTime.UtcNow;

        foreach (var item in items)
        {
            if (!patientsToUpdate.TryGetValue(item.PatientId, out var patient))
                throw new InvalidOperationException($"Không tìm thấy PatientId: {item.PatientId}");

            var canonicalDoc = item.DocumentNumber.Trim();
            var normalizedCountry = string.IsNullOrWhiteSpace(item.IssuingCountryCode)
                ? null
                : item.IssuingCountryCode.Trim().ToUpperInvariant();

            var expectedHmac = hasher.Hash(canonicalDoc, item.DocumentType, normalizedCountry, targetKeyVersion);
            var masked = hasher.Mask(canonicalDoc, item.DocumentType);

            var isAlreadyMigrated = patient.CitizenIdHash == expectedHmac &&
                                    patient.DocumentType == item.DocumentType &&
                                    patient.IssuingCountryCode == normalizedCountry &&
                                    patient.HashScheme == IdentityHashScheme.HmacCanonical &&
                                    patient.IdentityKeyVersion == targetKeyVersion &&
                                    patient.CitizenIdMasked == masked;

            if (isAlreadyMigrated)
            {
                alreadyUpToDate++;
                continue;
            }

            patient.CitizenIdHash = expectedHmac;
            patient.CitizenIdMasked = masked;
            patient.DocumentType = item.DocumentType;
            patient.IssuingCountryCode = normalizedCountry;
            patient.HashScheme = IdentityHashScheme.HmacCanonical;
            patient.IdentityKeyVersion = targetKeyVersion;
            patient.CitizenIdVersion++;
            patient.UpdatedAt = now;

            db.AuditLogs.Add(new AuditLog
            {
                UserId = actorUserId,
                Action = "MOD_PAT_IDENTITY_MIGRATED",
                EntityType = nameof(Patient),
                EntityId = patient.PatientId,
                Detail = JsonSerializer.Serialize(new
                {
                    patientId = patient.PatientId,
                    documentType = item.DocumentType.ToString(),
                    keyVersion = targetKeyVersion,
                    citizenIdVersion = patient.CitizenIdVersion
                }),
                CreatedAt = now
            });

            converted++;
        }

        await db.SaveChangesAsync(ct);
        await tx.CommitAsync(ct);

        return new IdentityConversionResult(items.Count, converted, alreadyUpToDate, IsDryRun: false);
    }

    private static string MaskDocument(string documentNumber)
    {
        if (documentNumber.Length <= 4)
            return new string('*', documentNumber.Length);

        var first = documentNumber[..Math.Min(3, documentNumber.Length - 3)];
        var last = documentNumber[^Math.Min(3, documentNumber.Length - first.Length)..];
        var middleMaskLen = documentNumber.Length - first.Length - last.Length;
        return first + new string('*', Math.Max(1, middleMaskLen)) + last;
    }

}
