namespace Dental.Domain.Constants;

/// <summary>
/// Cấu hình khung giờ và slot đặt lịch (DL-043, DL-050).
/// Giờ hoạt động: 08:00–11:30 và 13:30–16:30, slot 30 phút, tối đa 100 khách/slot.
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
