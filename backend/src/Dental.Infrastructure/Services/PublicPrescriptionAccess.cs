using System.Security.Cryptography;
using System.Text.Json;
using Dental.Application.Features.MOD_RX;
using Dental.Application.Interfaces;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Extensions.Caching.Memory;

namespace Dental.Infrastructure.Services;
public sealed class PublicPrescriptionAccess(IDataProtectionProvider provider, TimeProvider clock) : IPublicPrescriptionAccess, IDisposable
{
    private readonly IDataProtector protector = provider.CreateProtector("MOD_RX.PublicPrescription.View.v1");
    private readonly MemoryCache attempts = new(new MemoryCacheOptions { SizeLimit = 10000 });
    private readonly object gate = new();
    private sealed record Attempt(int Failures, DateTimeOffset ExpiresAt);
    private sealed record Grant(string Code, int PrescriptionId, int CitizenIdVersion, DateTimeOffset ExpiresAt);
    private Attempt? Current(string code) => attempts.TryGetValue<Attempt>(code, out var item) && item!.ExpiresAt > clock.GetUtcNow() ? item : null;
    public bool IsLocked(string code) { lock (gate) return Current(code) is { Failures: >= 5 }; }
    public bool RecordResult(string code, bool success)
    {
        lock (gate)
        {
            var state = Current(code);
            if (state is { Failures: >= 5 }) return false;
            if (success) { attempts.Remove(code); return true; }
            var next = new Attempt((state?.Failures ?? 0) + 1, clock.GetUtcNow().AddMinutes(15));
            // Clock check above supports deterministic tests; cache TTL also bounds memory in production.
            attempts.Set(code, next, new MemoryCacheEntryOptions { Size = 1, AbsoluteExpirationRelativeToNow = TimeSpan.FromMinutes(15) });
            return attempts.TryGetValue<Attempt>(code, out _) && next.Failures < 5;
        }
    }
    public PublicPrescriptionSession Issue(string code, int prescriptionId, int citizenIdVersion)
    {
        var expiry = clock.GetUtcNow().AddMinutes(15);
        return new(protector.Protect(JsonSerializer.Serialize(new Grant(code, prescriptionId, citizenIdVersion, expiry))), expiry);
    }
    public bool Validate(string code, int prescriptionId, int citizenIdVersion, string token)
    {
        try
        {
            var grant = JsonSerializer.Deserialize<Grant>(protector.Unprotect(token));
            return grant is not null && grant.Code == code && grant.PrescriptionId == prescriptionId &&
                grant.CitizenIdVersion == citizenIdVersion && grant.ExpiresAt > clock.GetUtcNow();
        }
        catch (Exception error) when (error is CryptographicException or JsonException or FormatException or ArgumentException) { return false; }
    }
    public void Dispose() => attempts.Dispose();
}
