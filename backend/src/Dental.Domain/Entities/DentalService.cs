using Dental.Domain.Common;

namespace Dental.Domain.Entities;

public sealed class DentalService : BaseEntity
{
    public int DentalServiceId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public int DurationMinutes { get; set; }
    public bool IsActive { get; set; } = true;

    public ICollection<ServicePrice> Prices { get; set; } = new List<ServicePrice>();
}
