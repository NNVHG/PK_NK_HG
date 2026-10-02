namespace Dental.Domain.Constants;

/// <summary>Trạng thái của một lần khám.</summary>
public static class VisitStatuses
{
    public const string Created = "Created";
    public const string InProgress = "InProgress";
    public const string Completed = "Completed";
    public const string Cancelled = "Cancelled";

    public static readonly string[] All = [Created, InProgress, Completed, Cancelled];
    public static readonly string[] Open = [Created, InProgress];
}
