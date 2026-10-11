namespace Dental.Domain.Entities;

/// <summary>MOD_FDI: chỉ định dịch vụ, bảo toàn tên và giá tại thời điểm chỉ định.</summary>
public sealed class VisitService
{
    public int Id { get; set; }
    public int VisitId { get; set; }
    public int ServiceId { get; set; }
    public int ServicePriceId { get; set; }
    public string ServiceCode { get; set; } = string.Empty;
    public string ServiceName { get; set; } = string.Empty;
    public int? ToothNumber { get; set; }
    public string? Surface { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public Visit Visit { get; set; } = null!;
    public DentalService Service { get; set; } = null!;
    public ServicePrice Price { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
