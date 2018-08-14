using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using RpcError = Nethereum.JsonRpc.Client.RpcError;
using RpcRequest = Nethereum.JsonRpc.Client.RpcRequest;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using Common.Logging;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json.Linq;

namespace Nethereum.JsonRpc.IpcClient
{
    public class IpcClient : IpcClientBase
    {
        private readonly object _lockingObject = new object();
        private readonly ILog _log;

        private NamedPipeClientStream _pipeClient;
      

        public IpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : base(ipcPath, jsonSerializerSettings)
        {
            _log = log;
        }

        private NamedPipeClientStream GetPipeClient()
        {
            try
            {
                if (_pipeClient == null || !_pipeClient.IsConnected)
                {
                    _pipeClient = new NamedPipeClientStream(IpcPath);
                    _pipeClient.Connect((int)ConnectionTimeout.TotalMilliseconds);
                }
            }
            catch (TimeoutException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout afer {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch
            {
                //Connection error we want to allow to retry.
                _pipeClient = null;
                throw;
            }
            return _pipeClient;
        }


        public int ReceiveBufferedResponse(NamedPipeClientStream client, byte[] buffer)
        {
            int bytesRead = 0;
            if (Task.Run(async () =>
                    bytesRead = await client.ReadAsync(buffer, 0, buffer.Length)
                ).Wait(ForceCompleteReadTotalMiliseconds))
            {
                return bytesRead;
            }
            else
            {
                return bytesRead;
            }
        }

        public MemoryStream ReceiveFullResponse(NamedPipeClientStream client)
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
                    GetPipeClient().Write(requestBytes, 0, requestBytes.Length);

                    using (var memoryData = ReceiveFullResponse(GetPipeClient()))
                    {
                        memoryData.Position = 0;
                        using (StreamReader streamReader = new StreamReader(memoryData))
                        using (JsonTextReader reader = new JsonTextReader(streamReader))
                        {
                            var serializer = JsonSerializer.Create(JsonSerializerSettings);
                            var message = serializer.Deserialize<TResponse>(reader);
                            logger.LogResponse(message);
                            return message;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
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
                if (disposing)
                    if (_pipeClient != null)
                    {
#if NET462
                        _pipeClient.Close();
#endif
                        _pipeClient.Dispose();
                    }

                disposedValue = true;
            }
        }
#endregion
    }
}
