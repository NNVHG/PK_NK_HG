namespace Dental.Domain.Entities;

// Operational history is append-only; it never replaces clinical prescription items.
public sealed class PrescriptionDispenseEvent
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public int PrescriptionVersion { get; set; }
    public string Action { get; set; } = string.Empty;
    public DateTime PerformedAt { get; set; }
    public int PerformedBy { get; set; }
    public string? Reason { get; set; }
    public bool ReceivedByProxy { get; set; }
    public string? ReceiverName { get; set; }
    public string? ReceiverPhone { get; set; }
    public bool ExpiryOverridden { get; set; }
}
