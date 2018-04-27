using System;
using System.IO;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Net.Sockets;
using Common.Logging;
using Nethereum.JsonRpc.Client;

namespace Nethereum.JsonRpc.IpcClient
{
    public class UnixIpcClient : IpcClientBase
    {
        private readonly object _lockingObject = new object();
        private readonly ILog _log;
        public UnixIpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : base(ipcPath, jsonSerializerSettings)
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
                        throw new TimeoutException();
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
                ).Wait(ForceCompleteReadTotalMiliseconds))
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

        protected override async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
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
                    var val = client.SendAsync(new ArraySegment<byte>(requestBytes, 0, requestBytes.Length), SocketFlags.None).Result;
#endif
                    using (var memoryStream = ReceiveFullResponse(client))
                    {
                        memoryStream.Position = 0;
                        using (var streamReader = new StreamReader(memoryStream))
                        using (var reader = new JsonTextReader(streamReader))
                        {
                            var serializer = JsonSerializer.Create(JsonSerializerSettings);
                            var message = serializer.Deserialize<TResponse>(reader);
                            logger.LogResponse(message);
                            return message;
                        }
                    }
                }
            
            } catch (Exception ex) {
                logger.LogException(ex);
                throw new RpcClientUnknownException("Error occurred when trying to send ipc requests(s)", ex);
            }
        }

#region IDisposable Support

        private bool disposedValue;

        protected override void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                disposedValue = true;
            }
        }

#endregion
    }
}
