using Estacionamento.Domain.Common;
using Microsoft.AspNetCore.Mvc;

namespace Estacionamento.Api.ErrorHandling;

public sealed class ExceptionHandlingMiddleware(
    RequestDelegate next,
    ILogger<ExceptionHandlingMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (DomainException exception)
        {
            var statusCode = exception.Kind switch
            {
                DomainErrorKind.Validation => StatusCodes.Status400BadRequest,
                DomainErrorKind.NotFound => StatusCodes.Status404NotFound,
                DomainErrorKind.Conflict => StatusCodes.Status409Conflict,
                DomainErrorKind.Forbidden => StatusCodes.Status403Forbidden,
                DomainErrorKind.RuleViolation => StatusCodes.Status422UnprocessableEntity,
                _ => StatusCodes.Status422UnprocessableEntity
            };

            await WriteProblemAsync(context, statusCode, exception.Message, exception.Kind.ToString());
        }
        catch (Exception exception)
        {
            logger.LogError(
                exception,
                "Unhandled request failure for {Method} {Path}. CorrelationId={CorrelationId}",
                context.Request.Method,
                context.Request.Path,
                context.TraceIdentifier);

            await WriteProblemAsync(
                context,
                StatusCodes.Status500InternalServerError,
                "Não foi possível concluir a operação. Informe o identificador de correlação ao suporte.",
                "UnexpectedError");
        }
    }

    private static Task WriteProblemAsync(
        HttpContext context,
        int statusCode,
        string detail,
        string errorCode)
    {
        if (context.Response.HasStarted)
        {
            return Task.CompletedTask;
        }

        context.Response.Clear();
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/problem+json";

        var problem = new ProblemDetails
        {
            Status = statusCode,
            Title = TitleFor(statusCode),
            Detail = detail,
            Instance = context.Request.Path
        };
        problem.Extensions["code"] = errorCode;
        problem.Extensions["correlationId"] = context.TraceIdentifier;

        return context.Response.WriteAsJsonAsync(problem);
    }

    private static string TitleFor(int statusCode) => statusCode switch
    {
        StatusCodes.Status400BadRequest => "Dados inválidos",
        StatusCodes.Status403Forbidden => "Acesso proibido",
        StatusCodes.Status404NotFound => "Recurso não encontrado",
        StatusCodes.Status409Conflict => "Conflito de estado",
        StatusCodes.Status422UnprocessableEntity => "Regra de negócio não satisfeita",
        _ => "Falha inesperada"
    };
}
