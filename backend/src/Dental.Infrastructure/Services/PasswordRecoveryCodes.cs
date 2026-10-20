using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.Extensions.Caching.Memory;

namespace Dental.Infrastructure.Services;

/// <summary>Single-clinic OTP challenges. Restart safely invalidates all pending codes; none are persisted or logged.</summary>
public sealed class PasswordRecoveryCodes(TimeProvider clock) : IPasswordRecoveryCodes, IDisposable
{
    private sealed class Challenge(Guid id, string digest, RecoveryAccount? account, DateTimeOffset createdAt)
    {
        public Guid Id { get; } = id;
        public string Digest { get; } = digest;
        public RecoveryAccount? Account { get; } = account;
        public DateTimeOffset CreatedAt { get; } = createdAt;
        public int Attempts { get; set; }
        public bool Consumed { get; set; }
    }
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly object gate = new();
    private string Digest(string value) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));

    public Result<RecoveryCode> Issue(string phone, RecoveryAccount? account)
    {
        lock (gate)
        {
            var phoneKey = Digest(phone);
            var now = clock.GetUtcNow();
            if (cache.TryGetValue<Challenge>(phoneKey, out var previous) && now - previous!.CreatedAt < TimeSpan.FromSeconds(60))
                return Result<RecoveryCode>.Failure(new Error("AUTH_062", "Vui lòng chờ đủ 60 giây trước khi yêu cầu mã mới."));
            var id = Guid.NewGuid();
            var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            var challenge = new Challenge(id, Digest(id.ToString("N") + code), account, now);
            cache.Set(phoneKey, challenge, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });
            if (!cache.TryGetValue<Challenge>(phoneKey, out var stored) || !ReferenceEquals(stored, challenge))
                return Result<RecoveryCode>.Failure(PasswordRecoveryService.Unavailable);
            return Result<RecoveryCode>.Success(new(id, code));
        }
    }

    public RecoveryAccount? Consume(Guid challengeId, string phone, string code)
    {
        lock (gate)
        {
            if (!cache.TryGetValue<Challenge>(Digest(phone), out var current) || current is null ||
                current.Id != challengeId || current.Consumed || current.Attempts >= 5 ||
                clock.GetUtcNow() - current.CreatedAt >= TimeSpan.FromMinutes(5)) return null;
            current.Attempts++;
            var candidate = Digest(challengeId.ToString("N") + code);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(candidate), Encoding.ASCII.GetBytes(current.Digest))) return null;
            current.Consumed = true;
            return current.Account;
        }
    }

    public void Revoke(string phone, Guid challengeId)
    {
        lock (gate)
            if (cache.TryGetValue<Challenge>(Digest(phone), out var current) && current!.Id == challengeId) current.Consumed = true;
    }
    public bool IsCurrent(string phone, Guid challengeId)
    {
        lock (gate)
            return cache.TryGetValue<Challenge>(Digest(phone), out var current) && current is not null &&
                current.Id == challengeId && !current.Consumed && current.Attempts < 5 &&
                clock.GetUtcNow() - current.CreatedAt < TimeSpan.FromMinutes(5);
    }
    public void Dispose() { cache.Dispose(); CryptographicOperations.ZeroMemory(key); }
}
