using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.RateLimiting;

public static class QueueVoiceRateLimits
{
    public const string Policy = "QueueVoice";
    public const string NumberQuery = "queueNumber";
    public const string ExpiryQuery = "expiresAtUtc";

    public static bool RequestsVoice(HttpRequest request)
        => request.Query.ContainsKey(NumberQuery) || request.Query.ContainsKey(ExpiryQuery);

    public static void Configure(RateLimiterOptions options)
    {
        options.AddPolicy(Policy, context =>
        {
            if (!RequestsVoice(context.Request))
                return RateLimitPartition.GetNoLimiter("queue-display-json");

            context.Response.Headers.CacheControl = "no-store";
            return RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ =>
                new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0, AutoReplenishment = true });
        });
    }
}
