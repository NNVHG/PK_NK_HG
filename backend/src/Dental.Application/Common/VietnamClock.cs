namespace Dental.Application.Common;

/// <summary>Cung cấp ngày hiện tại theo múi giờ Việt Nam.</summary>
public sealed class VietnamClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public VietnamClock(TimeProvider timeProvider)
    {
        _timeProvider = timeProvider;
        _timeZone = ResolveVietnamTimeZone();
    }

    public DateOnly Today
        => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone).DateTime);

    private static TimeZoneInfo ResolveVietnamTimeZone()
    {
        var timeZoneIds = OperatingSystem.IsWindows()
            ? new[] { "SE Asia Standard Time", "Asia/Ho_Chi_Minh" }
            : new[] { "Asia/Ho_Chi_Minh", "SE Asia Standard Time" };

        foreach (var timeZoneId in timeZoneIds)
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById(timeZoneId);
            }
            catch (TimeZoneNotFoundException)
            {
                // Thử tên múi giờ còn lại được .NET hỗ trợ trên hệ điều hành.
            }
            catch (InvalidTimeZoneException)
            {
                // Thử tên múi giờ còn lại nếu cấu hình hệ thống không hợp lệ.
            }
        }

        throw new TimeZoneNotFoundException("Không tìm thấy múi giờ Việt Nam trên hệ điều hành.");
    }
}
