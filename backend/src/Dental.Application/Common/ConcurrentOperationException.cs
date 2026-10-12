namespace Dental.Application.Common;

public sealed class ConcurrentOperationException : Exception
{
    public ConcurrentOperationException() : base("Concurrent operation; reload before retrying.") { }
}
