using System.Security.Cryptography;
using System.Text;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Dental.Infrastructure.Services;

/// <summary>
/// Triển khai băm định danh có khóa bí mật (HMAC-SHA256) theo DL-364.
/// Khóa bí mật đọc từ configuration (IdentitySecurity:HmacKey) hoặc fallback key cố định cho Dev/Test.
/// </summary>
public sealed class KeyedIdentityHasher : IKeyedIdentityHasher
{
    private readonly byte[] _hmacKey;

    public KeyedIdentityHasher(IConfiguration configuration)
    {
        var configuredKey = configuration["IdentitySecurity:HmacKey"]?.Trim();
        if (!string.IsNullOrEmpty(configuredKey) && configuredKey.Length >= 32)
        {
            _hmacKey = Encoding.UTF8.GetBytes(configuredKey);
        }
        else
        {
            // Khóa mặc định an toàn cho môi trường thử nghiệm
            _hmacKey = Encoding.UTF8.GetBytes("DentalClinicKeyedIdentitySecretKeyForHmacSha256_2026!#*");
        }
    }

    // Constructor cho phép test inject key trực tiếp
    public KeyedIdentityHasher(byte[] key)
    {
        _hmacKey = key ?? throw new ArgumentNullException(nameof(key));
    }

    public static string Canonicalize(string identifier, IdentityDocumentType documentType, string? nationalityCode)
    {
        var cleanedId = identifier?.Trim() ?? string.Empty;
        if (documentType == IdentityDocumentType.Passport)
        {
            var country = string.IsNullOrWhiteSpace(nationalityCode) ? IdentityDocumentConstants.DefaultCountryCode : nationalityCode.Trim().ToUpperInvariant();
            return $"PASSPORT:{country}:{cleanedId.ToUpperInvariant()}";
        }

        return $"CCCD:{cleanedId}";
    }

    public string Hash(string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null)
    {
        var canonical = Canonicalize(identifier, documentType, nationalityCode);
        return Convert.ToHexString(HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(canonical)));
    }

    public string Mask(string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId)
    {
        var id = identifier?.Trim() ?? string.Empty;
        if (id.Length <= 4)
            return new string('*', id.Length);

        if (id.Length == 12) // CCCD 12 số: 001******789
            return id[..3] + "******" + id[^3..];

        // Passport hoặc độ dài khác: 2 ký tự đầu + **** + 2 ký tự cuối
        if (id.Length >= 6)
            return id[..2] + new string('*', id.Length - 4) + id[^2..];

        return id[..1] + new string('*', id.Length - 2) + id[^1..];
    }

    public bool Matches(string? storedHash, string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || storedHash.Length != 64)
            return false;

        var storedBytes = Encoding.ASCII.GetBytes(storedHash.ToUpperInvariant());

        // 1. Kiểm tra khớp HMAC có khóa (DL-364)
        var newHmac = Hash(identifier, documentType, nationalityCode);
        if (CryptographicOperations.FixedTimeEquals(storedBytes, Encoding.ASCII.GetBytes(newHmac)))
            return true;

        // 2. Tương thích ngược: kiểm tra khớp SHA-256 legacy (DL-240) cho CCCD
        if (documentType == IdentityDocumentType.CitizenId)
        {
            var rawLegacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier.Trim())));
            if (CryptographicOperations.FixedTimeEquals(storedBytes, Encoding.ASCII.GetBytes(rawLegacyHash)))
                return true;
        }

        return false;
    }
}
