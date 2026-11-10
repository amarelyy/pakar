using Pakar.Application.Common;

namespace Pakar.Api.Endpoints;

/// <summary>Maps application results to HTTP responses; every failure is RFC 7807 problem details.</summary>
public static class OperationResultExtensions
{
    public static IResult ToHttpResult<T>(this OperationResult<T> result, Func<T, IResult>? onSuccess = null)
    {
        if (result.Succeeded)
            return onSuccess is null ? Results.Ok(result.Value) : onSuccess(result.Value!);

        return result.Status switch
        {
            OperationStatus.Invalid => Results.ValidationProblem(
                result.Errors?.ToDictionary(e => e.Key, e => e.Value) ?? new Dictionary<string, string[]>(),
                detail: result.Error),
            OperationStatus.NotFound => Results.Problem(result.Error, statusCode: StatusCodes.Status404NotFound, title: "Not found"),
            OperationStatus.Conflict => Results.Problem(result.Error, statusCode: StatusCodes.Status409Conflict, title: "Conflict"),
            OperationStatus.Forbidden => Results.Problem(result.Error, statusCode: StatusCodes.Status403Forbidden, title: "Forbidden"),
            _ => Results.Problem(result.Error, statusCode: StatusCodes.Status500InternalServerError)
        };
    }
}
