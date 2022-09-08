using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Sockets;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.JsonRpc.IpcClient
{
    public class UnixIpcClient : IpcClientBase
    {
        private readonly object _lockingObject = new object();
        private readonly ILogger _log;
        public UnixIpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null, ILogger log = null) : base(ipcPath, jsonSerializerSettings)
        {
            _log = log;
        }

        private Socket _socket;

        private Socket GetSocket()
        {
            try
            {
                if (_socket == null || !_socket.Connected)    
                {
                    var endPoint = new UnixDomainSocketEndPoint(IpcPath);
                    _socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.Unspecified);
                    if (!Task.Run(() =>
                        _socket.Connect(endPoint)).Wait(ConnectionTimeout))
                    {
                        throw new RpcClientTimeoutException($"Rpc timeout afer {ConnectionTimeout.TotalMilliseconds} milliseconds");
                    }
                }
            }
            catch
            {
                //Connection error we want to allow to retry.
                _socket = null;
                throw;
            }
            return _socket;
        }

        public int ReceiveBufferedResponse(Socket client, byte[] buffer)
        {
#if NET461
            int bytesRead = 0;
            if (Task.Run(() => 
                    bytesRead = client.Receive(buffer, SocketFlags.None)
                ).Wait(ForceCompleteReadTotalMiliseconds))
            {
                return bytesRead;
            }
            else
            {
                return bytesRead;
            }
#else
             int bytesRead = 0;
            if (Task.Run(async () => 
                    bytesRead = await client.ReceiveAsync(new ArraySegment<byte>(buffer), SocketFlags.None)
.ConfigureAwait(false)).Wait(ForceCompleteReadTotalMiliseconds))
            {
                return bytesRead;
            }
            else
            {
                return bytesRead;
            }
#endif
        }

        public MemoryStream ReceiveFullResponse(Socket client)
        {
            var readBufferSize = 512;
            var memoryStream = new MemoryStream();

            int bytesRead = 0;
            byte[] buffer = new byte[readBufferSize];
            bytesRead = ReceiveBufferedResponse(client, buffer);
            while (bytesRead > 0)
            {
                memoryStream.Write(buffer, 0, bytesRead);
                var lastByte = buffer[bytesRead - 1];

                if (lastByte == 10)  //return signalled with a line feed
                {
                    bytesRead = 0;
                }
                else
                {
                    bytesRead = ReceiveBufferedResponse(client, buffer);
                }
            }
            return memoryStream;
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            var logger = new RpcLogger(_log);
            try
            {
                lock (_lockingObject)
                {
                    var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                    var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
                    logger.LogRequest(rpcRequestJson);
                    var client = GetSocket();
                    client.SendBufferSize = requestBytes.Length;
#if NET461
                    var val = client.Send(requestBytes, SocketFlags.None);
#else
                    var val =
 client.SendAsync(new ArraySegment<byte>(requestBytes, 0, requestBytes.Length), SocketFlags.None).Result;
#endif
                    using (var memoryStream = ReceiveFullResponse(client))
                    {
                        memoryStream.Position = 0;
                        using (var streamReader = new StreamReader(memoryStream))
                        using (var reader = new JsonTextReader(streamReader))
                        {
                            var serializer = JsonSerializer.Create(JsonSerializerSettings);
                            var message = serializer.Deserialize<RpcResponseMessage>(reader);
                            logger.LogResponse(message);
                            return message;
                        }
                    }
                }

            }
            catch (Exception ex)
            {

                var exception = new RpcClientUnknownException("Error occurred when trying to send ipc requests(s): " + request.Method, ex);
                logger.LogException(exception);
                throw exception;
            }
        }    

#region IDisposable Support

        private bool _disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!_disposedValue)
            {
                _disposedValue = true;
            }
        }

#endregion
    }
}
