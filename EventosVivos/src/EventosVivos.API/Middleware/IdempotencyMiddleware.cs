using EventosVivos.Domain.Entities;
using EventosVivos.Domain.Interfaces;
using System.Security.Cryptography;
using System.Text;

namespace EventosVivos.API.Middleware;

public class IdempotencyMiddleware(RequestDelegate next, ILogger<IdempotencyMiddleware> logger)
{
    private static readonly string[] IdempotencyMethods = ["POST", "PUT", "PATCH"];

    public async Task InvokeAsync(HttpContext context, IIdempotencyRepository repo)
    {
        if (!IdempotencyMethods.Contains(context.Request.Method, StringComparer.OrdinalIgnoreCase)
            || !context.Request.Headers.TryGetValue("Idempotency-Key", out var keyHeader)
            || !Guid.TryParse(keyHeader, out var idempotencyKey))
        {
            await next(context);
            return;
        }

        var existing = await repo.GetAsync(idempotencyKey, context.RequestAborted);
        if (existing is not null)
        {
            logger.LogInformation("Returning cached idempotent response for key {Key}", idempotencyKey);
            context.Response.StatusCode = existing.ResponseStatusCode;
            context.Response.ContentType = existing.ResponseContentType;
            await context.Response.WriteAsync(existing.ResponseBody);
            return;
        }

        context.Request.EnableBuffering();
        var requestBody = await ReadBodyAsync(context.Request);
        var bodyHash = ComputeHash(requestBody);

        var originalBody = context.Response.Body;
        using var responseBuffer = new MemoryStream();
        context.Response.Body = responseBuffer;

        await next(context);

        responseBuffer.Seek(0, SeekOrigin.Begin);
        var responseBody = await new StreamReader(responseBuffer).ReadToEndAsync();

        responseBuffer.Seek(0, SeekOrigin.Begin);
        await responseBuffer.CopyToAsync(originalBody);
        context.Response.Body = originalBody;

        if (context.Response.StatusCode is >= 200 and < 300)
        {
            var record = new IdempotencyRecord
            {
                Id = idempotencyKey,
                RequestPath = context.Request.Path,
                RequestBodyHash = bodyHash,
                ResponseStatusCode = context.Response.StatusCode,
                ResponseBody = responseBody,
                ResponseContentType = context.Response.ContentType ?? "application/json",
                ExpiresAt = DateTime.UtcNow.AddHours(24)
            };

            try { await repo.SaveAsync(record, CancellationToken.None); }
            catch (Exception ex) { logger.LogWarning(ex, "Failed to save idempotency record for key {Key}", idempotencyKey); }
        }
    }

    private static async Task<string> ReadBodyAsync(HttpRequest request)
    {
        request.Body.Seek(0, SeekOrigin.Begin);
        var body = await new StreamReader(request.Body, leaveOpen: true).ReadToEndAsync();
        request.Body.Seek(0, SeekOrigin.Begin);
        return body;
    }

    private static string ComputeHash(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes)[..16];
    }
}
