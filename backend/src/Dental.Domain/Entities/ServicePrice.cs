using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Giá dịch vụ bất biến; thay đổi giá phải tạo bản ghi mới.</summary>
public sealed class ServicePrice : BaseEntity
{
    public int ServicePriceId { get; set; }
    public int ServiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public int CreatedByUserId { get; set; }

    public DentalService Service { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
