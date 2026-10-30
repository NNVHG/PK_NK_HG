using System.Collections.Frozen;
using System.Security.Cryptography;
using System.Text;
using Dental.Application.Interfaces;
using Dental.Domain.Constants;
using Dental.Domain.Enums;
using Microsoft.Extensions.Configuration;

namespace Dental.Infrastructure.Services;

/// <summary>
/// Triển khai băm định danh có khóa bí mật (HMAC-SHA256) theo DL-364 & DL-367.
/// Khóa bí mật bắt buộc đọc từ cấu hình riêng, không có khóa mặc định trong mã nguồn.
/// Hỗ trợ ghim phiên bản khóa hiện hành và đối chiếu qua nhiều phiên bản khóa (DL-367).
/// </summary>
public sealed class KeyedIdentityHasher : IKeyedIdentityHasher
{
    private readonly int _currentVersion;
    private readonly FrozenDictionary<int, byte[]> _keysByVersion;

    public int CurrentVersion => _currentVersion;

    public KeyedIdentityHasher(IConfiguration configuration)
    {
        var keys = new Dictionary<int, byte[]>();

        var keysSection = configuration.GetSection("IdentitySecurity:Keys");
        foreach (var child in keysSection.GetChildren())
        {
            if (int.TryParse(child.Key, out var version) && version > 0)
            {
                var val = child.Value?.Trim();
                if (!string.IsNullOrEmpty(val) && Encoding.UTF8.GetByteCount(val) >= 32)
                {
                    keys[version] = Encoding.UTF8.GetBytes(val);
                }
            }
        }

        var singleKey = configuration["IdentitySecurity:HmacKey"]?.Trim();
        if (!string.IsNullOrEmpty(singleKey) && Encoding.UTF8.GetByteCount(singleKey) >= 32)
        {
            keys.TryAdd(IdentityDocumentConstants.CurrentHmacKeyVersion, Encoding.UTF8.GetBytes(singleKey));
        }

        var configuredCurrentVersionStr = configuration["IdentitySecurity:CurrentVersion"]?.Trim();
        if (int.TryParse(configuredCurrentVersionStr, out var parsedCurrentVersion) && parsedCurrentVersion > 0)
        {
            _currentVersion = parsedCurrentVersion;
        }
        else
        {
            _currentVersion = keys.Keys.Count > 0 ? keys.Keys.Max() : IdentityDocumentConstants.CurrentHmacKeyVersion;
        }

        if (keys.Count == 0 || !_keysByVersionContainsKeyOrCurrentValid(keys, _currentVersion))
        {
            throw new InvalidOperationException("Thiếu khóa HMAC định danh hợp lệ trong cấu hình riêng.");
        }

        _keysByVersion = keys.ToFrozenDictionary();
    }

    private static bool _keysByVersionContainsKeyOrCurrentValid(Dictionary<int, byte[]> dict, int currentVer)
        => dict.ContainsKey(currentVer) && dict[currentVer].Length >= 32;

    public KeyedIdentityHasher(byte[] key, int version = IdentityDocumentConstants.CurrentHmacKeyVersion)
    {
        ArgumentNullException.ThrowIfNull(key);
        if (key.Length < 32) throw new ArgumentException("Khóa HMAC định danh phải có ít nhất 32 byte.", nameof(key));
        if (version <= 0) throw new ArgumentException("Phiên bản khóa phải là số nguyên dương.", nameof(version));

        _currentVersion = version;
        _keysByVersion = new Dictionary<int, byte[]> { [version] = (byte[])key.Clone() }.ToFrozenDictionary();
    }

    public KeyedIdentityHasher(IReadOnlyDictionary<int, byte[]> keys, int? currentVersion = null)
    {
        ArgumentNullException.ThrowIfNull(keys);
        if (keys.Count == 0) throw new ArgumentException("Danh sách khóa HMAC không được rỗng.", nameof(keys));

        var copy = new Dictionary<int, byte[]>();
        foreach (var (v, k) in keys)
        {
            if (v <= 0) throw new ArgumentException("Phiên bản khóa phải là số nguyên dương.", nameof(keys));
            ArgumentNullException.ThrowIfNull(k);
            if (k.Length < 32) throw new ArgumentException($"Khóa HMAC phiên bản {v} phải có ít nhất 32 byte.", nameof(keys));
            copy[v] = (byte[])k.Clone();
        }

        _currentVersion = currentVersion ?? copy.Keys.Max();
        if (!copy.ContainsKey(_currentVersion))
            throw new ArgumentException($"Phiên bản khóa hiện hành {_currentVersion} không tồn tại trong danh sách khóa.", nameof(currentVersion));

        _keysByVersion = copy.ToFrozenDictionary();
    }

    public static string Canonicalize(string identifier, IdentityDocumentType documentType, string? nationalityCode)
    {
        var cleanedId = identifier?.Trim() ?? string.Empty;
        if (documentType == IdentityDocumentType.Passport)
        {
            var country = nationalityCode?.Trim();
            if (!ValidPassportCountry(country))
                throw new ArgumentException("Mã quốc gia cấp hộ chiếu phải gồm 2–3 chữ cái.", nameof(nationalityCode));
            return $"PASSPORT:{country!.ToUpperInvariant()}:{cleanedId.ToUpperInvariant()}";
        }

        return $"CCCD:{cleanedId}";
    }

    public string Hash(string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null, int? keyVersion = null)
    {
        var targetVersion = keyVersion ?? _currentVersion;
        if (!_keysByVersion.TryGetValue(targetVersion, out var keyBytes))
            throw new InvalidOperationException($"Không tìm thấy khóa HMAC cho phiên bản {targetVersion}.");

        var canonical = Canonicalize(identifier, documentType, nationalityCode);
        return Convert.ToHexString(HMACSHA256.HashData(keyBytes, Encoding.UTF8.GetBytes(canonical)));
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

    public bool Matches(string? storedHash, string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null, int? expectedKeyVersion = null)
    {
        if (string.IsNullOrWhiteSpace(storedHash) || storedHash.Length != 64)
            return false;
        if (documentType == IdentityDocumentType.Passport && !ValidPassportCountry(nationalityCode?.Trim()))
            return false;

        var storedBytes = Encoding.ASCII.GetBytes(storedHash.ToUpperInvariant());

        // 1. Nếu caller chỉ định expectedKeyVersion cụ thể, chỉ đối chiếu với phiên bản đó
        if (expectedKeyVersion.HasValue)
        {
            if (_keysByVersion.TryGetValue(expectedKeyVersion.Value, out var specificKey))
            {
                var canonical = Canonicalize(identifier, documentType, nationalityCode);
                var hmac = Convert.ToHexString(HMACSHA256.HashData(specificKey, Encoding.UTF8.GetBytes(canonical)));
                if (CryptographicOperations.FixedTimeEquals(storedBytes, Encoding.ASCII.GetBytes(hmac)))
                    return true;
            }
        }
        else
        {
            // Đối chiếu trước với khóa hiện hành
            var canonical = Canonicalize(identifier, documentType, nationalityCode);
            var canonicalBytes = Encoding.UTF8.GetBytes(canonical);

            if (_keysByVersion.TryGetValue(_currentVersion, out var currentKey))
            {
                var currentHmac = Convert.ToHexString(HMACSHA256.HashData(currentKey, canonicalBytes));
                if (CryptographicOperations.FixedTimeEquals(storedBytes, Encoding.ASCII.GetBytes(currentHmac)))
                    return true;
            }

            // Đối chiếu lần lượt với các phiên bản khóa cũ khác đã cấu hình
            foreach (var (ver, key) in _keysByVersion)
            {
                if (ver == _currentVersion) continue;
                var pastHmac = Convert.ToHexString(HMACSHA256.HashData(key, canonicalBytes));
                if (CryptographicOperations.FixedTimeEquals(storedBytes, Encoding.ASCII.GetBytes(pastHmac)))
                    return true;
            }
        }

        // 2. Tương thích ngược: kiểm tra khớp SHA-256 legacy (DL-240) cho CCCD (khi expectedKeyVersion không ghim)
        if (documentType == IdentityDocumentType.CitizenId && (!expectedKeyVersion.HasValue || expectedKeyVersion.Value == 0))
        {
            var rawLegacyHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(identifier.Trim())));
            if (CryptographicOperations.FixedTimeEquals(storedBytes, Encoding.ASCII.GetBytes(rawLegacyHash)))
                return true;
        }

        return false;
    }

    private static bool ValidPassportCountry(string? country) =>
        country is { Length: 2 or 3 } && country.All(char.IsAsciiLetter);
}
