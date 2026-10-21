using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
namespace Dental.Api.RateLimiting;
public static class BookingRequestRateLimits
{
    public const string Read="BookingRequestRead",Otp="BookingRequestOtp",Submit="BookingRequestSubmit";
    public static void Configure(RateLimiterOptions options)
    {
        Add(options,Read,120,60);Add(options,Otp,10,300);Add(options,Submit,30,60);
    }
    private static void Add(RateLimiterOptions options,string name,int limit,int seconds)=>options.AddPolicy(name,context=>RateLimitPartition.GetFixedWindowLimiter(context.Connection.RemoteIpAddress?.ToString()??"unknown",_=>new FixedWindowRateLimiterOptions{PermitLimit=limit,Window=TimeSpan.FromSeconds(seconds),QueueLimit=0,AutoReplenishment=true}));
}
