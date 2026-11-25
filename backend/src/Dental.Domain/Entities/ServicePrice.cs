using System.Text.Json.Serialization;
using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Giá dịch vụ bất biến; thay đổi giá phải tạo bản ghi mới.</summary>
public sealed class ServicePrice : BaseEntity
{
    public int ServicePriceId { get; set; }
    [JsonIgnore]
    public int ServiceId { get; set; }
    public decimal Amount { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public int CreatedByUserId { get; set; }

    [JsonIgnore]
    public DentalService Service { get; set; } = null!;
    [JsonIgnore]
    public User CreatedByUser { get; set; } = null!;
}
