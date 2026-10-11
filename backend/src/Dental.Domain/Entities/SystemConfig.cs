namespace Dental.Domain.Entities;

// Minimal storage needed by DL-173; administration of other settings belongs to MOD_MST.
public sealed class SystemConfig
{
    public string Key { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
}
