using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.Staff;
using Microsoft.Extensions.Caching.Memory;

namespace Dental.Infrastructure.Services;

public sealed class StaffPhoneChangeProofs(TimeProvider clock) : IStaffPhoneChangeProofs, IDisposable
{
    private sealed record Entry(StaffPhoneChangeTarget Target, StaffPhoneChangePending Pending, byte[] CodeHash)
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
    public Result<StaffPhoneChangeDispatch> Issue(StaffPhoneChangeTarget target)
    {
        lock (gate)
        {
            var cooldown = "cooldown:" + target.UserId;
            if (cache.Get<DateTimeOffset?>(cooldown) is { } last && last > clock.GetUtcNow())
                return Result<StaffPhoneChangeDispatch>.Failure(new("STAFF_PHONE_OTP_RATE", "Chờ ít nhất 60 giây trước khi Admin gửi lại."));
            var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6");
            var pending = new StaffPhoneChangePending(Guid.NewGuid(), target.NewPhone, clock.GetUtcNow().AddMinutes(5));
            var options = new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromMinutes(5));
            cache.Set(target.UserId, new Entry(target, pending, Hash(pending.ChallengeId + ":" + code)), options);
            cache.Set(cooldown, clock.GetUtcNow().AddSeconds(60), new MemoryCacheEntryOptions().SetSize(1).SetAbsoluteExpiration(TimeSpan.FromSeconds(60)));
            if (Current(target.UserId)?.Pending.ChallengeId != pending.ChallengeId)
                return Result<StaffPhoneChangeDispatch>.Failure(StaffPhoneChangeService.Unavailable);
            return Result<StaffPhoneChangeDispatch>.Success(new(pending, code));
        }
    }
    public StaffPhoneChangePending? Pending(int userId) { lock (gate) return Current(userId)?.Pending; }
    public StaffPhoneChangeTarget? Consume(int userId, StaffPhoneChangeConfirmation request)
    {
        lock (gate)
        {
            var entry = Current(userId);
            if (entry is null || entry.Pending.ChallengeId != request.ChallengeId) return null;
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
