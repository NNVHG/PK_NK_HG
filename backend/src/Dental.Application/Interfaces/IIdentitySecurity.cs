using Dental.Domain.Enums;

namespace Dental.Application.Interfaces;

/// <summary>
/// Giao diện băm định danh có khóa (HMAC-SHA256) và đối chiếu linh hoạt tương thích SHA-256 legacy (DL-364).
/// </summary>
public interface IKeyedIdentityHasher
{
    /// <summary>Băm số CCCD hoặc Hộ chiếu (kèm mã quốc tịch) bằng HMAC-SHA256 có khóa.</summary>
    string Hash(string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null);

    /// <summary>Tạo mặt nạ hiển thị (mask) cho số định danh.</summary>
    string Mask(string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId);

    /// <summary>Đối chiếu số định danh với hash lưu trong CSDL (hỗ trợ cả HMAC mới lẫn SHA-256 legacy).</summary>
    bool Matches(string? storedHash, string identifier, IdentityDocumentType documentType = IdentityDocumentType.CitizenId, string? nationalityCode = null);
}

/// <summary>
/// Quản lý khóa đăng nhập tạm thời theo tài khoản / định danh (DL-365).
/// 5 lần nhập sai trong 15 phút -> khóa tạm 15 phút.
/// </summary>
public interface ILoginLockoutService
{
    /// <summary>Kiểm tra xem định danh đăng nhập hiện có đang bị khóa tạm thời không.</summary>
    bool IsLocked(string identifier, out TimeSpan remainingTime);

    /// <summary>Ghi nhận một lần đăng nhập thất bại.</summary>
    bool RecordFailedAttempt(string identifier, out bool nowLocked, out TimeSpan lockoutDuration);

    /// <summary>Xóa bộ đếm thất bại khi đăng nhập thành công.</summary>
    void ResetAttempts(string identifier);
}
