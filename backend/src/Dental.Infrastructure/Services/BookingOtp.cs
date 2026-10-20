using System.Security.Cryptography;
using System.Text;
using Dental.Application.Common;
using Dental.Application.Features.MOD_APP;
using Dental.Infrastructure.Repositories;
using Microsoft.Extensions.Caching.Memory;
namespace Dental.Infrastructure.Services;

public interface IBookingOtpDelivery
{
    bool IsConfigured{get;}
    Task<bool> SendAsync(string phone,string code,CancellationToken ct);
}
/// <summary>OTP purpose isolated from recovery/activation. No plaintext codes, phones or intent data in logs/cache.</summary>
public sealed class BookingOtp(IBookingOtpDelivery delivery,TimeProvider clock):IBookingOtp,IDisposable
{
    private sealed record Challenge(Guid Id,string Digest,string Intent,DateTimeOffset Issued){public int Attempts;public bool Used;}
    private readonly MemoryCache cache=new(new MemoryCacheOptions{SizeLimit=10000});
    private readonly byte[] key=RandomNumberGenerator.GetBytes(32);private readonly object gate=new();
    private string Hash(string value)=>Convert.ToHexString(HMACSHA256.HashData(key,Encoding.UTF8.GetBytes(value)));
    public bool IsConfigured=>delivery.IsConfigured;
    public async Task<Result<BookingOtpResponse>> IssueAsync(BookingIntent intent,int? actor,CancellationToken ct)
    {
        if(!IsConfigured)return Result<BookingOtpResponse>.Failure(new Error("APP_REQUEST_UNAVAILABLE","Kênh xác thực đặt lịch chưa sẵn sàng."));
        var code=RandomNumberGenerator.GetInt32(1000000).ToString("D6",System.Globalization.CultureInfo.InvariantCulture);Challenge challenge;
        lock(gate)
        {
            var phone=Hash(intent.Phone);var now=clock.GetUtcNow();
            if(cache.TryGetValue<Challenge>(phone,out var old)&&now-old!.Issued<TimeSpan.FromSeconds(60))return Result<BookingOtpResponse>.Failure(new Error("APP_REQUEST_RATE","Chờ đủ 60 giây trước khi xin mã mới."));
            var id=Guid.NewGuid();challenge=new(id,Hash(id.ToString("N")+code),BookingRequestRepository.Digest(intent,actor),now);
            cache.Set(phone,challenge,new MemoryCacheEntryOptions{Size=1,AbsoluteExpirationRelativeToNow=TimeSpan.FromMinutes(5)});
            if(!cache.TryGetValue<Challenge>(phone,out var stored)||!ReferenceEquals(stored,challenge))return Result<BookingOtpResponse>.Failure(new Error("APP_REQUEST_UNAVAILABLE","Kênh xác thực đang bận."));
        }
        if(!await delivery.SendAsync(intent.Phone,code,ct))
        {lock(gate){challenge.Used=true;}return Result<BookingOtpResponse>.Failure(new Error("APP_REQUEST_UNAVAILABLE","Chưa gửi được mã xác thực. Thử lại sau hoặc liên hệ phòng khám."));}
        return Result<BookingOtpResponse>.Success(new(challenge.Id));
    }
    public bool Consume(SubmitBookingRequest request,int? actor)
    {
        lock(gate)
        {
            if(!cache.TryGetValue<Challenge>(Hash(request.Intent.Phone),out var c)||c is null||c.Id!=request.ChallengeId||c.Used||c.Attempts>=5||clock.GetUtcNow()-c.Issued>=TimeSpan.FromMinutes(5))return false;
            c.Attempts++;
            var digest=Hash(c.Id.ToString("N")+request.Code);
            if(c.Intent!=BookingRequestRepository.Digest(request.Intent,actor)||!CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(digest),Encoding.ASCII.GetBytes(c.Digest)))return false;
            c.Used=true;return true;
        }
    }
    public void Dispose(){cache.Dispose();CryptographicOperations.ZeroMemory(key);}
}
