using Dental.Domain.Common;

namespace Dental.Domain.Entities;

/// <summary>Bản ghi sinh hiệu do nhân viên nhập, không diễn giải lâm sàng.</summary>
public sealed class VitalSignRecord : BaseEntity
{
    public int VitalSignRecordId { get; set; }
    public int PatientId { get; set; }
    public int VisitId { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? PulseBpm { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? Note { get; set; }
    public int RecordedByUserId { get; set; }

    public Patient Patient { get; set; } = null!;
    public Visit Visit { get; set; } = null!;
    public User RecordedByUser { get; set; } = null!;
}
