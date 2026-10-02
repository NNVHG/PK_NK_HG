namespace Dental.Application.Features.VitalSigns.DTOs;

public sealed class RecordVitalSignsRequest
{
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? PulseBpm { get; set; }
    public decimal? TemperatureC { get; set; }
    public string? Note { get; set; }
}
