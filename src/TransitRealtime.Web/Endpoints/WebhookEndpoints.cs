using TransitRealtime.Web.Services;

namespace TransitRealtime.Web.Endpoints;

public static class WebhookEndpoints
{
    public static void MapWebhookEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapPost("/webhook/{serviceId:guid}", async (
                Guid serviceId,
                HttpRequest request,
                WebhookDispatcher dispatcher) =>
            {
                if (!request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) ||
                    string.IsNullOrWhiteSpace(apiKeyHeader))
                {
                    return Results.Unauthorized();
                }

                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync();
                var sourceIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();

                var result = await dispatcher.DispatchAsync(serviceId, apiKeyHeader.ToString(), payload, sourceIp);

                return result switch
                {
                    DispatchResult.Ok => Results.Ok(new { status = "delivered" }),
                    DispatchResult.ServiceNotFound => Results.NotFound(),
                    DispatchResult.Unauthorized => Results.Unauthorized(),
                    _ => Results.Problem(statusCode: 500),
                };
            })
            .RequireRateLimiting("webhook");

        // Targeted delivery: only clients that joined this exact recipientKey via JoinChannel receive it.
        app.MapPost("/webhook/{serviceId:guid}/{recipientKey}", async (
                Guid serviceId,
                string recipientKey,
                HttpRequest request,
                WebhookDispatcher dispatcher) =>
            {
                if (!request.Headers.TryGetValue("X-Api-Key", out var apiKeyHeader) ||
                    string.IsNullOrWhiteSpace(apiKeyHeader))
                {
                    return Results.Unauthorized();
                }

                using var reader = new StreamReader(request.Body);
                var payload = await reader.ReadToEndAsync();
                var sourceIp = request.HttpContext.Connection.RemoteIpAddress?.ToString();

                var result = await dispatcher.DispatchAsync(serviceId, apiKeyHeader.ToString(), payload, sourceIp, recipientKey);

                return result switch
                {
                    DispatchResult.Ok => Results.Ok(new { status = "delivered" }),
                    DispatchResult.ServiceNotFound => Results.NotFound(),
                    DispatchResult.Unauthorized => Results.Unauthorized(),
                    _ => Results.Problem(statusCode: 500),
                };
            })
            .RequireRateLimiting("webhook");
    }
}
