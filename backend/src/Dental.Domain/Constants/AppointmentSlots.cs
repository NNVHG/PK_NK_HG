namespace Dental.Domain.Constants;

/// <summary>
/// Trần công suất DL050; ValidSlots giữ lưới lịch sử cho tương thích.
/// Luồng đặt/đổi mới dùng ClinicSchedule từ SystemConfigs theo DL174/316.
/// </summary>
public static class AppointmentSlots
{
    public const int MaxBookingsPerSlot = 100; // DL-050

    public static readonly TimeOnly[] ValidSlots =
    [
        new(8, 0),
        new(8, 30),
        new(9, 0),
        new(9, 30),
        new(10, 0),
        new(10, 30),
        new(11, 0),
        new(13, 30),
        new(14, 0),
        new(14, 30),
        new(15, 0),
        new(15, 30),
        new(16, 0)
    ];

    public static bool IsValidSlot(TimeOnly slot)
        => ValidSlots.Contains(slot);
}
