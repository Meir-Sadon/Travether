using Microsoft.AspNetCore.Mvc;

namespace Travether.Api.Api;

/// <summary>
/// Errors leave the API as problem details with a stable machine-readable <c>code</c>
/// (e.g. <c>EmailTaken</c>); the client maps the code to a translated message.
/// </summary>
public static class ApiError
{
    public static ObjectResult Create(int status, string code, string? detail = null)
    {
        var problem = new ProblemDetails { Status = status, Title = code, Detail = detail };
        problem.Extensions["code"] = code;
        return new ObjectResult(problem) { StatusCode = status };
    }

    public static ObjectResult BadRequest(string code, string? detail = null) => Create(StatusCodes.Status400BadRequest, code, detail);

    public static ObjectResult Unauthorized(string code) => Create(StatusCodes.Status401Unauthorized, code);

    public static ObjectResult Forbidden(string code = "Forbidden") => Create(StatusCodes.Status403Forbidden, code);

    public static ObjectResult NotFound(string code = "NotFound") => Create(StatusCodes.Status404NotFound, code);

    public static ObjectResult Conflict(string code) => Create(StatusCodes.Status409Conflict, code);

    public static ObjectResult TooManyRequests(string code = "TooManyRequests") => Create(StatusCodes.Status429TooManyRequests, code);
}
