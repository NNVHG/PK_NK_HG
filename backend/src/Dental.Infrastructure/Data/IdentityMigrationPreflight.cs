using System.Text.RegularExpressions;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Dental.Infrastructure.Data;

/// <summary>Kiểm kê chỉ đọc trước khi đối soát dữ liệu định danh cũ.</summary>
public static class IdentityMigrationPreflight
{
    private static readonly Regex HashPattern = new("^[0-9A-Fa-f]{64}$", RegexOptions.Compiled);
    private static readonly Regex CountryPattern = new(IdentityDocumentConstants.NationalityCodeRegex, RegexOptions.Compiled);

    public static async Task<IdentityInventory> InspectAsync(DentalDbContext db, CancellationToken ct = default)
    {
        // Chỉ giữ dữ liệu trong bộ nhớ của lượt kiểm kê; kết quả không chứa hash, mặt nạ hay giấy tờ.
        var rows = await db.Patients.AsNoTracking()
            .Select(patient => new
            {
                patient.CitizenIdHash,
                patient.CitizenIdMasked,
                patient.DocumentType,
                patient.IssuingCountryCode,
                patient.HashScheme,
                patient.IdentityKeyVersion
            })
            .ToListAsync(ct);

        var missingHash = 0;
        var malformedHash = 0;
        var unclassifiedHash = 0;
        var invalidMetadata = 0;
        var nonCurrentKeyVersion = 0;
        var currentHmac = 0;
        var validHashes = new List<string>();

        foreach (var row in rows)
        {
            if (string.IsNullOrWhiteSpace(row.CitizenIdHash))
            {
                missingHash++;
                continue;
            }

            if (!HashPattern.IsMatch(row.CitizenIdHash))
            {
                malformedHash++;
                continue;
            }

            validHashes.Add(row.CitizenIdHash.ToUpperInvariant());
            if (row.DocumentType is null && row.HashScheme is null && row.IdentityKeyVersion is null &&
                row.IssuingCountryCode is null)
            {
                // Không thể phân biệt SHA-256 cũ với HMAC thiếu metadata chỉ từ 64 ký tự hex.
                unclassifiedHash++;
                continue;
            }

            var validDocument = row.DocumentType switch
            {
                IdentityDocumentType.CitizenId => row.IssuingCountryCode is null,
                IdentityDocumentType.Passport => row.IssuingCountryCode is not null &&
                    CountryPattern.IsMatch(row.IssuingCountryCode),
                _ => false
            };
            if (row.HashScheme != IdentityHashScheme.HmacCanonical ||
                row.IdentityKeyVersion is null or <= 0 || !validDocument ||
                string.IsNullOrWhiteSpace(row.CitizenIdMasked) || !row.CitizenIdMasked.Contains('*'))
            {
                invalidMetadata++;
                continue;
            }

            if (row.IdentityKeyVersion != IdentityDocumentConstants.CurrentHmacKeyVersion)
                nonCurrentKeyVersion++;
            else
                currentHmac++;
        }

        var duplicateHashGroups = validHashes.GroupBy(hash => hash, StringComparer.Ordinal)
            .Count(group => group.Count() > 1);
        return new IdentityInventory(rows.Count, missingHash, malformedHash, duplicateHashGroups,
            unclassifiedHash, invalidMetadata, nonCurrentKeyVersion, currentHmac);
    }
}

/// <summary>Chỉ gồm số lượng tổng hợp; không chứa dữ liệu định danh.</summary>
public sealed record IdentityInventory(
    int TotalPatients,
    int MissingHash,
    int MalformedHash,
    int DuplicateHashGroups,
    int UnclassifiedHash,
    int InvalidMetadata,
    int NonCurrentKeyVersion,
    int CurrentHmac);
