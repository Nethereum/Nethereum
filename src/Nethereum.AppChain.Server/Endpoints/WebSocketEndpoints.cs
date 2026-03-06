using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Rpc.Subscriptions;

namespace Nethereum.AppChain.Server.Endpoints
{
    public static class WebSocketEndpoints
    {
        public static void MapWebSocketEndpoint(this WebApplication app, WebSocketRpcHandler wsHandler)
        {
            app.Map("/ws", async (HttpContext httpContext) =>
            {
                if (!httpContext.WebSockets.IsWebSocketRequest)
                {
                    httpContext.Response.StatusCode = 400;
                    await httpContext.Response.WriteAsync("WebSocket connections only");
                    return;
                }

                var logger = httpContext.RequestServices.GetService<ILoggerFactory>()?.CreateLogger("WebSocket");

                var ws = await httpContext.WebSockets.AcceptWebSocketAsync();
                logger?.LogInformation("[WS] Client connected");

                await wsHandler.HandleConnectionAsync(ws);

                logger?.LogInformation("[WS] Client disconnected");
            });
        }
    }
}
