using System.Net.WebSockets;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public static class ClientWebSocketExtensions
    {
        public static bool IsNullOrNotOpen(this ClientWebSocket clientWebSocket)
        {
            return clientWebSocket?.State != WebSocketState.Open;
        }

        public static bool IsNotNullAndOpen(this ClientWebSocket clientWebSocket)
        {
            return clientWebSocket?.State == WebSocketState.Open;
        }
    }
}
