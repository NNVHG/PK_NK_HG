using Microsoft.AspNetCore.RateLimiting;
namespace Dental.Api.RateLimiting;

public static class DentalImageRateLimits
{
    public const string Upload = "MOD_IMG_Upload";
    public static void Configure(RateLimiterOptions options)
    {
        // Bound in-memory multipart buffers across this single-clinic server.
        options.AddConcurrencyLimiter(Upload, limit => { limit.PermitLimit = 4; limit.QueueLimit = 0; });
    }
}
