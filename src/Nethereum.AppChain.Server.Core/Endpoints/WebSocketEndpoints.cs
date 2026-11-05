using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nethereum.ChainNode.Hosting.Rpc;
using Nethereum.CoreChain.Rpc.Subscriptions;

namespace Nethereum.AppChain.Server.Endpoints
{
    public static class WebSocketEndpoints
    {
        public static void MapWebSocketEndpoint(this WebApplication app, WebSocketRpcHandler wsHandler, RpcHttpOptions options)
        {
            var hostPolicy = options.HostPolicy;
            var originPolicy = options.OriginPolicy;

            app.Map("/ws", async (HttpContext httpContext) =>
            {
                if (!httpContext.WebSockets.IsWebSocketRequest)
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("WebSocket connections only");
                    return;
                }

                var logger = httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("WebSocket");

                if (!hostPolicy.IsAllowed(httpContext.Request.Host.Value))
                {
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                string? origin = httpContext.Request.Headers.TryGetValue("Origin", out var originValues)
                    ? originValues.ToString()
                    : null;
                if (!originPolicy.IsAllowed(origin))
                {
                    logger?.LogDebug("[WS] Rejected connection from a disallowed origin");
                    httpContext.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return;
                }

                var ws = await httpContext.WebSockets.AcceptWebSocketAsync();
                logger?.LogDebug("[WS] Client connected");

                await wsHandler.HandleConnectionAsync(ws, httpContext.RequestAborted);

                logger?.LogDebug("[WS] Client disconnected");
            });
        }
    }
}
