using MediatR;
using Microsoft.Extensions.Logging;

namespace EventosVivos.Application.Behaviors;

public class LoggingBehavior<TRequest, TResponse>(ILogger<LoggingBehavior<TRequest, TResponse>> logger)
    : IPipelineBehavior<TRequest, TResponse>
    where TRequest : notnull
{
    public async Task<TResponse> Handle(TRequest request, RequestHandlerDelegate<TResponse> next, CancellationToken ct)
    {
        var requestName = typeof(TRequest).Name;
        logger.LogInformation("Handling {RequestName}", requestName);

        var sw = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var response = await next(ct);
            sw.Stop();
            logger.LogInformation("Handled {RequestName} in {Elapsed}ms", requestName, sw.ElapsedMilliseconds);
            return response;
        }
        catch (Exception ex)
        {
            sw.Stop();
            logger.LogError(ex, "Error handling {RequestName} after {Elapsed}ms", requestName, sw.ElapsedMilliseconds);
            throw;
        }
    }
}
