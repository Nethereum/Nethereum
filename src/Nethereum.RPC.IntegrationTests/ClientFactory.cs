using Nethereum.JsonRpc.Client;
//using Nethereum.JsonRpc.IpcClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;
using Nethereum.RPC.Tests.Testers;
using Nethereum.JsonRpc.WebSocketClient;
using Nethereum.JsonRpc.WebSocketStreamingClient;
using Moq;
using Common.Logging;
using System.IO;

namespace Nethereum.RPC.Tests
{
    public class ClientFactory
    {
        static ClientFactory()
        {
            StreamingWebSocketClient.ConnectionTimeout = TimeSpan.FromSeconds(60);
        }

        public static IClient GetClient(TestSettings settings)
        {
           var url = settings.GetRPCUrl();
           return new RpcClient(new Uri(url)); 
        }

        public static IStreamingClient GetStreamingClient(TestSettings settings)
        {
            var url = settings.GetWSRpcUrl();

            Mock<ILog> mockLog = CreateLog();

            return new StreamingWebSocketClient(url, log: mockLog.Object);
        }

        private static Mock<ILog> CreateLog()
        {
            var file = Path.Combine(Path.GetTempPath(), "StreamingWebSocketClientErrors.txt");

            if(!File.Exists(file)) File.CreateText(file);

            var mockLog = new Mock<ILog>();
            mockLog.Setup((l) => l.IsErrorEnabled).Returns(true);
            mockLog.Setup((l) => l.IsTraceEnabled).Returns(true);

            mockLog
                .Setup(l => l.Error(It.IsAny<object>(), It.IsAny<Exception>()))
                .Callback<object, Exception>((msg, innerEx) =>
                {
                    File.AppendAllLines(file, new[] {msg.ToString(), innerEx.ToString() });
                });

            mockLog
                .Setup(l => l.Error(It.IsAny<object>()))
                .Callback<object>((msg) =>
                {
                    File.AppendAllLines(file, new[] { msg.ToString() });
                });

            return mockLog;
        }
    }

}
