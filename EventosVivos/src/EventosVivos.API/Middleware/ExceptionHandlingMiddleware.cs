using EventosVivos.Application.Common;
using EventosVivos.Domain.Exceptions;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using System.Net;
using System.Text.Json;

namespace EventosVivos.API.Middleware;

public class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> logger, IHostEnvironment env)
{
    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            await HandleExceptionAsync(context, ex);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var (statusCode, title) = exception switch
        {
            ValidationException       => (HttpStatusCode.BadRequest,            I18n.TitleValidation),
            NotFoundException         => (HttpStatusCode.NotFound,              I18n.TitleNotFound),
            DomainException           => (HttpStatusCode.UnprocessableEntity,   I18n.TitleDomain),
            ConflictException         => (HttpStatusCode.Conflict,              I18n.TitleConflict),
            UnauthorizedAccessException => (HttpStatusCode.Unauthorized,        I18n.TitleUnauthorized),
            _                         => (HttpStatusCode.InternalServerError,   I18n.TitleInternal)
        };

        logger.LogError(exception, "Unhandled exception: {Message}", exception.Message);

        var problem = new ProblemDetails
        {
            Status = (int)statusCode,
            Title  = title,
            Type   = $"https://httpstatuses.io/{(int)statusCode}"
        };

        if (exception is ValidationException validationEx)
        {
            problem.Extensions["errors"] = validationEx.Errors
                .GroupBy(e => e.PropertyName)
                .ToDictionary(g => g.Key, g => g.Select(e => e.ErrorMessage).ToArray());
        }
        else if (exception is NotFoundException notFoundEx)
        {
            problem.Detail = I18n.NotFoundEntity(notFoundEx.EntityName, notFoundEx.EntityKey);
        }
        else
        {
            problem.Detail = exception.Message;
        }

        if (env.IsDevelopment())
        {
            problem.Extensions["stackTrace"]    = exception.StackTrace;
            problem.Extensions["exceptionType"] = exception.GetType().FullName;
        }

        context.Response.StatusCode  = (int)statusCode;
        context.Response.ContentType = "application/problem+json";

        await context.Response.WriteAsync(JsonSerializer.Serialize(problem, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        }));
    }
}
