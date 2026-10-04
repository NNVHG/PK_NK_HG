using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;

namespace Dental.Api.RateLimiting;

public static class AuthRateLimitPolicies
{
    public const string Login = "AuthLogin";
    public const string Register = "AuthRegister";

    public static void Configure(RateLimiterOptions options, IConfiguration configuration)
    {
        options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
        options.OnRejected = async (context, cancellationToken) =>
        {
            context.HttpContext.Response.ContentType = "application/json";
            await context.HttpContext.Response.WriteAsJsonAsync(
                new
                {
                    code = "AUTH_009",
                    message = "Bạn đã gửi quá nhiều yêu cầu. Vui lòng thử lại sau.",
                },
                cancellationToken);
        };

        // [CẦN XÁC NHẬN] Sau reverse proxy, RemoteIpAddress có thể là IP proxy; không cấu hình forwarded headers ở đây.
        AddIpPolicy(
            options,
            configuration,
            Login,
            "RateLimiting:Auth:Login:PermitLimit",
            "RateLimiting:Auth:Login:WindowSeconds",
            permitLimitDefault: 10);
        AddIpPolicy(
            options,
            configuration,
            Register,
            "RateLimiting:Auth:Register:PermitLimit",
            "RateLimiting:Auth:Register:WindowSeconds",
            permitLimitDefault: 5);
    }

    private static void AddIpPolicy(
        RateLimiterOptions options,
        IConfiguration configuration,
        string policyName,
        string permitLimitKey,
        string windowSecondsKey,
        int permitLimitDefault)
    {
        var permitLimit = configuration.GetValue(permitLimitKey, permitLimitDefault);
        var windowSeconds = configuration.GetValue(windowSecondsKey, 60);

        options.AddPolicy(policyName, context =>
            RateLimitPartition.GetFixedWindowLimiter(
                context.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = permitLimit,
                    Window = TimeSpan.FromSeconds(windowSeconds),
                    QueueLimit = 0,
                    AutoReplenishment = true,
                }));
    }
}
