using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.MOD_AUTH;
using Microsoft.Extensions.Caching.Memory;

namespace Dental.Infrastructure.Services;

public sealed class PatientActivationProofs(TimeProvider clock) : IPatientActivationProofs, IDisposable
{
    private sealed record Challenge(Guid Id, string PhoneKey, string Digest, ActivationTarget? Target, DateTimeOffset Issued)
    { public int Attempts; public bool Used; }
    private sealed record Ticket(ActivationTarget Target, DateTimeOffset Issued) { public bool Used; }
    private readonly MemoryCache cache = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly byte[] key = RandomNumberGenerator.GetBytes(32);
    private readonly object gate = new();
    private string Hash(string value) => Convert.ToHexString(HMACSHA256.HashData(key, Encoding.UTF8.GetBytes(value)));
    private void Store(string cacheKey, object value) => cache.Set(cacheKey, value,
        new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(5) });

    public Result<ActivationDispatch> Issue(string phone, ActivationTarget? target)
    {
        lock (gate)
        {
            var phoneKey = "phone/" + Hash(phone); var now = clock.GetUtcNow();
            if (cache.TryGetValue<Challenge>(phoneKey, out var previous))
            {
                if (now - previous!.Issued < TimeSpan.FromSeconds(60))
                    return Result<ActivationDispatch>.Failure(new Error("AUTH_ACTIVATION_RATE", "Chờ đủ 60 giây trước khi xin mã mới."));
                previous.Used = true;
            }
            var id = Guid.NewGuid(); var code = RandomNumberGenerator.GetInt32(1000000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
            var challenge = new Challenge(id, phoneKey, Hash(id.ToString("N") + code), target, now);
            Store(phoneKey, challenge); Store("challenge/" + id, challenge);
            if (!cache.TryGetValue<Challenge>(phoneKey, out var stored) || !ReferenceEquals(stored, challenge) ||
                !cache.TryGetValue<Challenge>("challenge/" + id, out var byId) || !ReferenceEquals(byId, challenge))
                return Result<ActivationDispatch>.Failure(PatientActivationService.Unavailable);
            return Result<ActivationDispatch>.Success(new(phone, id, code));
        }
    }
    public ActivationTarget? ConsumeCode(ActivationVerification request)
    {
        lock (gate)
        {
            if (!cache.TryGetValue<Challenge>("challenge/" + request.ChallengeId, out var c) || c is null || c.Used || c.Attempts >= 5 ||
                clock.GetUtcNow() - c.Issued >= TimeSpan.FromMinutes(5) ||
                !cache.TryGetValue<Challenge>(c.PhoneKey, out var current) || !ReferenceEquals(c, current)) return null;
            c.Attempts++;
            var digest = Hash(c.Id.ToString("N") + request.Code);
            if (!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(digest), Encoding.ASCII.GetBytes(c.Digest))) return null;
            c.Used = true; return c.Target;
        }
    }
    public Result<ActivationGrant> Grant(ActivationTarget target)
    {
        lock (gate)
        {
            var token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32));
            var ticket = new Ticket(target, clock.GetUtcNow()); var cacheKey = "complete/" + Hash(token);
            Store(cacheKey, ticket);
            return cache.TryGetValue<Ticket>(cacheKey, out var stored) && ReferenceEquals(stored, ticket)
                ? Result<ActivationGrant>.Success(new(token)) : Result<ActivationGrant>.Failure(PatientActivationService.Unavailable);
        }
    }
    public ActivationTarget? ConsumeGrant(string token)
    {
        lock (gate)
        {
            if (!cache.TryGetValue<Ticket>("complete/" + Hash(token), out var t) || t is null || t.Used || clock.GetUtcNow() - t.Issued >= TimeSpan.FromMinutes(5)) return null;
            t.Used = true; return t.Target;
        }
    }
    public bool IsCurrent(string phone, Guid challengeId)
    {
        lock (gate) return cache.TryGetValue<Challenge>("phone/" + Hash(phone), out var c) && c is not null && c.Id == challengeId && !c.Used && c.Attempts < 5 && clock.GetUtcNow() - c.Issued < TimeSpan.FromMinutes(5);
    }
    public void Revoke(string phone, Guid challengeId)
    {
        lock (gate) if (cache.TryGetValue<Challenge>("phone/" + Hash(phone), out var c) && c is not null && c.Id == challengeId) c.Used = true;
    }
    public void Dispose() { cache.Dispose(); CryptographicOperations.ZeroMemory(key); }
}
