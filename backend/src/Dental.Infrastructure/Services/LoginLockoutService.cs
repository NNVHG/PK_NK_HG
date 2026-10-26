using System.Security.Cryptography;
using System.Text;
using Dental.Application.Interfaces;
using Microsoft.Extensions.Caching.Memory;

namespace Dental.Infrastructure.Services;

/// <summary>
/// Quản lý khóa tạm chống brute-force đăng nhập theo DL-365.
/// 5 lần sai trong 15 phút -> tạm chặn 15 phút.
/// Dùng HMAC hash identifier để lưu trong cache memory nhằm bảo vệ quyền riêng tư (không lưu raw phone/CCCD).
/// </summary>
public sealed class LoginLockoutService : ILoginLockoutService, IDisposable
{
    private sealed class LockoutEntry
    {
        public int FailedAttempts { get; set; }
        public DateTimeOffset FirstAttemptAt { get; set; }
        public DateTimeOffset? LockedUntil { get; set; }
    }

    private readonly IMemoryCache _cache;
    private readonly TimeProvider _clock;
    private readonly byte[] _hmacKey = RandomNumberGenerator.GetBytes(32);
    private readonly object _gate = new();

    public const int MaxAttempts = 5;
    public static readonly TimeSpan LockoutWindow = TimeSpan.FromMinutes(15);
    public static readonly TimeSpan LockoutDuration = TimeSpan.FromMinutes(15);

    public LoginLockoutService(TimeProvider clock)
    {
        _clock = clock ?? TimeProvider.System;
        _cache = new MemoryCache(new MemoryCacheOptions { SizeLimit = 10000 });
    }

    private string CacheKey(string identifier)
    {
        var normalized = identifier?.Trim().ToUpperInvariant() ?? string.Empty;
        var digest = HMACSHA256.HashData(_hmacKey, Encoding.UTF8.GetBytes(normalized));
        return "lockout/" + Convert.ToHexString(digest);
    }

    public bool IsLocked(string identifier, out TimeSpan remainingTime)
    {
        remainingTime = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(identifier)) return false;

        lock (_gate)
        {
            var key = CacheKey(identifier);
            if (_cache.TryGetValue<LockoutEntry>(key, out var entry) && entry is not null)
            {
                var now = _clock.GetUtcNow();
                if (entry.LockedUntil.HasValue && entry.LockedUntil.Value > now)
                {
                    remainingTime = entry.LockedUntil.Value - now;
                    return true;
                }
            }
            return false;
        }
    }

    public bool RecordFailedAttempt(string identifier, out bool nowLocked, out TimeSpan lockoutDuration)
    {
        nowLocked = false;
        lockoutDuration = TimeSpan.Zero;
        if (string.IsNullOrWhiteSpace(identifier)) return false;

        lock (_gate)
        {
            var key = CacheKey(identifier);
            var now = _clock.GetUtcNow();

            if (!_cache.TryGetValue<LockoutEntry>(key, out var entry) || entry is null)
            {
                entry = new LockoutEntry
                {
                    FailedAttempts = 1,
                    FirstAttemptAt = now
                };
            }
            else
            {
                // Nếu cửa sổ theo dõi 15 phút đã trôi qua kể từ lần sai đầu tiên, làm mới cửa sổ
                if (now - entry.FirstAttemptAt > LockoutWindow && !entry.LockedUntil.HasValue)
                {
                    entry.FailedAttempts = 1;
                    entry.FirstAttemptAt = now;
                    entry.LockedUntil = null;
                }
                else
                {
                    entry.FailedAttempts++;
                }
            }

            if (entry.FailedAttempts >= MaxAttempts)
            {
                entry.LockedUntil = now.Add(LockoutDuration);
                nowLocked = true;
                lockoutDuration = LockoutDuration;
            }

            _cache.Set(key, entry, new MemoryCacheEntryOptions
            {
                Size = 1,
                AbsoluteExpirationRelativeToNow = LockoutDuration.Add(TimeSpan.FromMinutes(5))
            });

            return true;
        }
    }

    public void ResetAttempts(string identifier)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return;
        lock (_gate)
        {
            _cache.Remove(CacheKey(identifier));
        }
    }

    public void Dispose()
    {
        _cache.Dispose();
        CryptographicOperations.ZeroMemory(_hmacKey);
    }
}
