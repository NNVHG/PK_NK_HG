namespace Dental.Application.Common;

/// <summary>
/// Wrapper kết quả nghiệp vụ.
/// Service trả Result&lt;T&gt; thay vì throw exception cho lỗi nghiệp vụ.
/// Chỉ throw exception thật sự cho lỗi hệ thống (DB down, config thiếu…).
/// </summary>
public class Result<T>
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public T? Value { get; }
    public Error Error { get; }

    private Result(T value)
    {
        IsSuccess = true;
        Value = value;
        Error = Error.None;
    }

    private Result(Error error)
    {
        IsSuccess = false;
        Value = default;
        Error = error;
    }

    public static Result<T> Success(T value) => new(value);
    public static Result<T> Failure(Error error) => new(error);
    public static Result<T> Failure(string code, string message) => new(new Error(code, message));

    public static Result<T> Ok(T value) => Success(value);
    public static Result<T> Fail(Error error) => Failure(error);
    public static Result<T> Fail(string code, string message) => Failure(code, message);
}

/// <summary>
/// Phiên bản không có giá trị trả về (chỉ thành công/thất bại).
/// </summary>
public class Result
{
    public bool IsSuccess { get; }
    public bool IsFailure => !IsSuccess;
    public Error Error { get; }

    private Result(bool success, Error? error = null)
    {
        IsSuccess = success;
        Error = error ?? Error.None;
    }

    public static Result Success() => new(true);
    public static Result Failure(Error error) => new(false, error);
    public static Result Failure(string code, string message) => new(false, new Error(code, message));

    public static Result Ok() => Success();
    public static Result Fail(Error error) => Failure(error);
    public static Result Fail(string code, string message) => Failure(code, message);
}
