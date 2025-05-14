using System;
using System.Net.Http;

namespace Nethereum.JsonRpc.SystemTextJsonRpcClient
{
    public static class RpcHttpHandlerFactory
    {
        public static int MaxConnectionsPerServer { get; set; } = 20;
        public static TimeSpan ConnectionLifetime { get; set; } = TimeSpan.FromMinutes(10);
        public static TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(5);

        public static SocketsHttpHandler Create()
        {
            return new SocketsHttpHandler
            {
                PooledConnectionLifetime = ConnectionLifetime,
                PooledConnectionIdleTimeout = IdleTimeout,
                MaxConnectionsPerServer = MaxConnectionsPerServer
            };
        }
    }
}