using System;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;
using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using RpcError = Nethereum.JsonRpc.Client.RpcError;
using RpcRequest = Nethereum.JsonRpc.Client.RpcRequest;
using System.Net.Sockets;
using System.Net;
using System.Diagnostics;
using Newtonsoft.Json.Linq;

namespace Nethereum.JsonRpc.IpcClient
{
    public class IpcClient : IpcClientBase
    {
        private readonly object _lockingObject = new object();

        private NamedPipeClientStream _pipeClient;

        public IpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null) : base(ipcPath, jsonSerializerSettings)
        {

        }

        private NamedPipeClientStream GetPipeClient()
        {
            try
            {
                if (_pipeClient == null || !_pipeClient.IsConnected)
                {
                    _pipeClient = new NamedPipeClientStream(IpcPath);
                    _pipeClient.Connect();
                }
            }
            catch
            {
                //Connection error we want to allow to retry.
                _pipeClient = null;
                throw;
            }
            return _pipeClient;
        }

        protected override async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            try
            {
                lock (_lockingObject)
                {
                    var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                    var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);

                    GetPipeClient().Write(requestBytes, 0, requestBytes.Length);

                    using (StreamReader streamReader = new StreamReader(GetPipeClient()))
                    using (JsonTextReader reader = new JsonTextReader(streamReader))
                    {
                        if (TryRead(reader))
                        {
                            string content = ReadJson(reader);
                            return JsonConvert.DeserializeObject<TResponse>(content, JsonSerializerSettings);
                        }
                    }
                    throw new RpcClientUnknownException(
                                 $"Unable to parse response from the ipc server");
                }

            }
            catch (Exception ex) when (!(ex is RpcClientException) && !(ex is RpcException))
            {
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
