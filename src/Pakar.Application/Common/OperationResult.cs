namespace Pakar.Application.Common;

/// <summary>Outcome categories that the API maps to HTTP status codes.</summary>
public enum OperationStatus
{
    Ok,
    Invalid,
    NotFound,
    Conflict,
    Forbidden
}

/// <summary>
/// Result of an application operation: either a value or a failure the caller can show.
/// Expected failures (validation, missing rows, business rules) are returned, not thrown.
/// </summary>
public sealed class OperationResult<T>
{
    private OperationResult(OperationStatus status, T? value, string? error, IReadOnlyDictionary<string, string[]>? errors)
    {
        Status = status;
        Value = value;
        Error = error;
        Errors = errors;
    }

    public OperationStatus Status { get; }
    public T? Value { get; }

    /// <summary>Human-readable reason for a failure.</summary>
    public string? Error { get; }

    /// <summary>Field errors for <see cref="OperationStatus.Invalid"/>, keyed by request property name.</summary>
    public IReadOnlyDictionary<string, string[]>? Errors { get; }

    public bool Succeeded => Status == OperationStatus.Ok;

    public static OperationResult<T> Ok(T value) => new(OperationStatus.Ok, value, null, null);

    public static OperationResult<T> Invalid(string field, string message) =>
        new(OperationStatus.Invalid, default, message, new Dictionary<string, string[]> { [field] = [message] });

    public static OperationResult<T> Invalid(IReadOnlyDictionary<string, string[]> errors) =>
        new(OperationStatus.Invalid, default, "One or more fields are invalid.", errors);

    public static OperationResult<T> NotFound(string message) => new(OperationStatus.NotFound, default, message, null);
    public static OperationResult<T> Conflict(string message) => new(OperationStatus.Conflict, default, message, null);
    public static OperationResult<T> Forbidden(string message) => new(OperationStatus.Forbidden, default, message, null);
}
