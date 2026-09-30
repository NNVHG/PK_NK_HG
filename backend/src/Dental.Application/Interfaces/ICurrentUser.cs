namespace Dental.Application.Interfaces;

public interface ICurrentUser
{
    int? UserId { get; }
    string? RoleCode { get; }
    bool IsAuthenticated { get; }
}
