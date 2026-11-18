using FoodAssistant.Contracts.Common;

namespace FoodAssistant.Server.Api.Errors;

/// <summary>Fallback <c>errorCode</c> for problems that do not name one (framework errors, empty status responses).</summary>
internal static class StatusErrorCodes
{
    public static string For(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => ErrorCodes.BadRequest,
        StatusCodes.Status401Unauthorized => ErrorCodes.Unauthorized,
        StatusCodes.Status403Forbidden => ErrorCodes.Forbidden,
        StatusCodes.Status404NotFound => ErrorCodes.NotFound,
        StatusCodes.Status405MethodNotAllowed => ErrorCodes.MethodNotAllowed,
        StatusCodes.Status409Conflict => ErrorCodes.Conflict,
        StatusCodes.Status412PreconditionFailed => ErrorCodes.PreconditionFailed,
        StatusCodes.Status413PayloadTooLarge => ErrorCodes.PayloadTooLarge,
        StatusCodes.Status415UnsupportedMediaType => ErrorCodes.UnsupportedMediaType,
        StatusCodes.Status422UnprocessableEntity => ErrorCodes.RuleViolated,
        StatusCodes.Status428PreconditionRequired => ErrorCodes.PreconditionRequired,
        StatusCodes.Status429TooManyRequests => ErrorCodes.RateLimited,
        StatusCodes.Status503ServiceUnavailable => ErrorCodes.ServiceUnavailable,
        >= 500 => ErrorCodes.InternalError,
        _ => ErrorCodes.RequestFailed,
    };
}
