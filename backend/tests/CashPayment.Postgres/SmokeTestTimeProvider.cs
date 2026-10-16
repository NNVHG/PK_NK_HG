using Dental.Application.Common;

// Dùng cùng một ngày và giờ tiếp đón cho fixture và dịch vụ trong test host.
// JWT vẫn dùng giờ hệ thống; các ca kiểm tra mốc 23:30 giữ đồng hồ riêng.
internal sealed class SmokeTestTimeProvider : TimeProvider
{
    public static TimeProvider Instance { get; } = new SmokeTestTimeProvider();

    private readonly DateTimeOffset _utcNow;

    private SmokeTestTimeProvider()
    {
        var today = new VietnamClock(TimeProvider.System).Today;
        _utcNow = new DateTimeOffset(today.Year, today.Month, today.Day, 2, 0, 0, TimeSpan.Zero);
    }

    public override DateTimeOffset GetUtcNow() => _utcNow;
}
