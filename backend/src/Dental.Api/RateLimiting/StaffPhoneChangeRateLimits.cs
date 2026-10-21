using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.RateLimiting;
public static class StaffPhoneChangeRateLimits
{
    public const string Request = "StaffPhoneChangeRequest", Read = "StaffPhoneChangeRead", Confirm = "StaffPhoneChangeConfirm";
    public static void Configure(RateLimiterOptions options)
    {
        var previous = options.OnRejected;
        options.OnRejected = async (context, ct) => {
            if (context.HttpContext.Request.Path.StartsWithSegments("/api/auth/staff-phone-change") || context.HttpContext.Request.Path.Value?.EndsWith("/phone-change") == true)
                context.HttpContext.Response.Headers.CacheControl = "no-store";
            if (previous is not null) await previous(context, ct);
        };
        Add(options, Request, 10, 300); Add(options, Read, 30, 60); Add(options, Confirm, 10, 60);
    }
    private static void Add(RateLimiterOptions options, string name, int limit, int seconds) => options.AddPolicy(name, context =>
        RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString() ?? "unknown", _ => new FixedWindowRateLimiterOptions {
            PermitLimit = limit, Window = TimeSpan.FromSeconds(seconds), QueueLimit = 0, AutoReplenishment = true
        }));
}
