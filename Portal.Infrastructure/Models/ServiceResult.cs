namespace Portal.Infrastructure.Models;

/// <summary>
/// Represents the outcome of a service operation with optional error message.
/// </summary>
public class ServiceResult
{
    public bool Success { get; set; }
    public string? Message { get; set; }
    public int? Id { get; set; }

    public static ServiceResult Ok() => new() { Success = true };
    public static ServiceResult Ok(int id) => new() { Success = true, Id = id };
    public static ServiceResult Fail(string message) => new() { Success = false, Message = message };
}

/// <summary>
/// Represents the outcome of a service operation that carries a typed data payload on success.
/// </summary>
public class ServiceResult<T> : ServiceResult
{
    public T? Data { get; set; }

    public static ServiceResult<T> Ok(T data) => new() { Success = true, Data = data };
    public static new ServiceResult<T> Fail(string message) => new() { Success = false, Message = message };
}
