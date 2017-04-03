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

namespace Nethereum.JsonRpc.IpcClient
{
    public class IpcClient : ClientBase, IDisposable
    {
        private readonly string _ipcPath;

        private readonly object _lockingObject = new object();

        private NamedPipeClientStream _pipeClient;

        public IpcClient(string ipcPath, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this._ipcPath = ipcPath;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        private NamedPipeClientStream GetPipeClient()
        {
            lock (_lockingObject)
            {
                try
                {
                    if (_pipeClient == null || !_pipeClient.IsConnected)
                    {
                        _pipeClient = new NamedPipeClientStream(_ipcPath);

                        _pipeClient.Connect();
                    }
                }
                catch
                {
                    //Connection error we want to allow to retry.
                    _pipeClient = null;
                    throw;
                }
            }

            return _pipeClient;
        }

        protected override async Task<T> SendInnerRequestAync<T>(RpcRequest request, string route = null)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, request.RawParameters))
                    .ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        protected override async Task<T> SendInnerRequestAync<T>(string method, string route = null,
            params object[] paramList)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(Configuration.DefaultRequestId, method, paramList))
                    .ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        private void HandleRpcError(RpcResponse response)
        {
            if (response.HasError)
                throw new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
        }

        public override async Task SendRequestAsync(RpcRequest request, string route = null)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, request.RawParameters))
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }

        public override async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var response =
                await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(
                        new EdjCase.JsonRpc.Core.RpcRequest(Configuration.DefaultRequestId, method, paramList))
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }

        private async Task<byte[]> ReadResponseStream(NamedPipeClientStream pipeClientStream)
        {
            var buffer = new byte[1024];

            using (var ms = new MemoryStream())
            {
                while (true)
                {
                    //if the total number of bytes matches 1024 for the last Read, it will wait for the next read forever as we don't have a flag for completeness
                    //a wait is in place for this with a 10 second (maybe too long..)
                    //if timesout (false returned) we have to close the pipestream and return the memory stream
                    var read = 0;
                    if (Task.Run(
                        async () =>
                        {
                            read = await pipeClientStream.ReadAsync(buffer, 0, buffer.Length).ConfigureAwait(false);
                        }
                    ).Wait(10000))
                    {
                        ms.Write(buffer, 0, read);
                        if (read < 1024)
                            return ms.ToArray();
                    }
                    else
                    {
                        pipeClientStream.Close();
                        return ms.ToArray();
                    }
                }
            }
        }

        private async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            try
            {
                var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
                await GetPipeClient().WriteAsync(requestBytes, 0, requestBytes.Length).ConfigureAwait(false);

                var responseBytes = await ReadResponseStream(GetPipeClient()).ConfigureAwait(false);
                if (responseBytes == null) throw new RpcClientUnknownException("Invalid response / null");
                var totalMegs = responseBytes.Length / 1024f / 1024f;
                if (totalMegs > 10)
                    throw new RpcClientUnknownException("Response exceeds 10MB it will be impossible to parse it");
                var responseJson = Encoding.UTF8.GetString(responseBytes);

                try
                {
                    return JsonConvert.DeserializeObject<TResponse>(responseJson, JsonSerializerSettings);
                }
                catch (JsonSerializationException)
                {
                    var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(responseJson, JsonSerializerSettings);
                    if (rpcResponse == null)
                        throw new RpcClientUnknownException(
                            $"Unable to parse response from the ipc server. Response Json: {responseJson}");
                    throw rpcResponse.Error.CreateException();
                }
            }
            catch (Exception ex) when (!(ex is RpcClientException) && !(ex is RpcException))
            {
                throw new RpcClientUnknownException("Error occurred when trying to send ipc requests(s)", ex);
            }
        }

        #region IDisposable Support

        private bool disposedValue;

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                    if (_pipeClient != null)
                    {
                        _pipeClient.Close();
                        _pipeClient.Dispose();
                    }

                disposedValue = true;
            }
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}