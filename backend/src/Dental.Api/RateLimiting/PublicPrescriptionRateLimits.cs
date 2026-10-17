using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace Dental.Api.RateLimiting;
public static class PublicPrescriptionRateLimits
{
    public const string Policy = "PublicPrescriptionLookup";
    public static void Configure(RateLimiterOptions options) =>
        options.AddPolicy(Policy, context => {
            context.Response.Headers.CacheControl = "no-store";
            return RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
        });
}
