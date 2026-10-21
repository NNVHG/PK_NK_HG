using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.Extensions.Caching.Memory;

namespace Dental.Infrastructure.Services;

public sealed class PatientPhoneChangeProofs(TimeProvider clock) : IPatientPhoneChangeProofs, IDisposable
{
    private sealed record Entry(PatientPhoneTarget Target, PatientPhonePending Pending, byte[] CodeHash)
    { public int WrongAttempts { get; set; } }
    private readonly object gate = new();
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private byte[] Hash(string value) => HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value));
    private Entry? Current(int userId)
    {
        var entry = cache.Get<Entry>(userId);
        if (entry is not null && entry.Pending.ExpiresAtUtc <= clock.GetUtcNow()) { cache.Remove(userId); return null; }
        return entry;
    }
    public Result<PatientPhoneDispatch> Issue(PatientPhoneTarget target)
    {
        lock (gate)
        {
            var cooldown = "cooldown:" + target.UserId;
            if (cache.Get<DateTimeOffset?>(cooldown) is { } last && last > clock.GetUtcNow())
                return Result<PatientPhoneDispatch>.Failure(new("PAT_PHONE_RATE", "Chờ ít nhất 60 giây trước khi gửi lại OTP."));
            var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6");
            var pending = new PatientPhonePending(Guid.NewGuid(), target.NewPhone, clock.GetUtcNow().AddMinutes(5));
            var options = new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            cache.Set(target.UserId, new Entry(target, pending, Hash(pending.ChallengeId + ":" + code)), options);
            cache.Set(cooldown, clock.GetUtcNow().AddSeconds(60), new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromSeconds(60)));
            if (Current(target.UserId)?.Pending.ChallengeId != pending.ChallengeId)
                return Result<PatientPhoneDispatch>.Failure(PatientPhoneChangeService.Unavailable);
            return Result<PatientPhoneDispatch>.Success(new(pending, code));
        }
    }
    public PatientPhoneTarget? Consume(int userId, PatientPhoneConfirmation request)
    {
        lock (gate)
        {
            var entry = Current(userId);
            if (entry is null || entry.Pending.ChallengeId != request.ChallengeId || entry.Target.ReviewStamp != request.ReviewStamp) return null;
            if (!CryptographicOperations.FixedTimeEquals(entry.CodeHash, Hash(request.ChallengeId + ":" + request.Code)))
            { if (++entry.WrongAttempts >= 5) cache.Remove(userId); return null; }
            cache.Remove(userId);
            return entry.Target;
        }
    }
    public void Revoke(int userId, Guid challengeId)
    { lock (gate) { if (Current(userId)?.Pending.ChallengeId == challengeId) cache.Remove(userId); } }
    public void Dispose() { cache.Dispose(); CryptographicOperations.ZeroMemory(key); }
}
