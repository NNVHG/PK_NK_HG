namespace Dental.Domain.Entities;

/// <summary>MOD_BIL: mẫu giảm giá do Admin cấu hình (DL-119).</summary>
public sealed class DiscountPreset
{
    public int Id { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string DiscountType { get; set; } = string.Empty;
    public decimal Value { get; set; }
    public bool IsActive { get; set; } = true;
}
