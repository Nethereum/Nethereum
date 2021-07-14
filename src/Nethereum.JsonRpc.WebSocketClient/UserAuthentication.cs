using System;
using System.Text;
using Nethereum.JsonRpc.WebSocketStreamingClient;

namespace Nethereum.JsonRpc.WebSocketClient
{
    public static class UserAuthentication
    {
        public static string GetBasicAuthentication(string userName, string password)
        {
            var byteArray = Encoding.UTF8.GetBytes(userName + ":" + password);
            return "Basic " + Convert.ToBase64String(byteArray);
        }

        public static void SetBasicAuthenticationHeader<T>(this StreamingWebSocketClient streamingWebSocketClient, string userName, string password)
        {
            streamingWebSocketClient.RequestHeaders.Add("AUTHORIZATION", GetBasicAuthentication(userName, password) );
        }

        public static void SetBasicAuthenticationHeader<T>(this WebSocketClient webSocketClient, string userName, string password)
        {
            webSocketClient.RequestHeaders.Add("AUTHORIZATION", GetBasicAuthentication(userName, password));
        }
    }
}