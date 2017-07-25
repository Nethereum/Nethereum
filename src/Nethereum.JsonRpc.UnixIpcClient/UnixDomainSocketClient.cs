using Nethereum.JsonRpc.Client;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using EdjCase.JsonRpc.Client;
using EdjCase.JsonRpc.Core;
using RpcError = Nethereum.JsonRpc.Client.RpcError;
using RpcRequest = Nethereum.JsonRpc.Client.RpcRequest;

namespace Nethereum.JsonRpc.UnixIpcClient
{
    public class UnixDomainSocketClient : IClient, IDisposable
    {
        private readonly string _ipcPath;
        private readonly UnixEndPoint _unixEndPoint;

        private readonly JsonSerializerSettings _jsonSerializationSettings = new JsonSerializerSettings
        {
            NullValueHandling = NullValueHandling.Ignore
        };

        public UnixDomainSocketClient(string ipcPath)
        {
            _ipcPath = ipcPath;

            _unixEndPoint = new UnixEndPoint(_ipcPath);
        }

        public RequestInterceptor OverridingRequestInterceptor { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        public async Task<T> SendRequestAsync<T>(RpcRequest request, string route = null)
        {
            var response = await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, request.RawParameters)).ConfigureAwait(false);
            HandleRpcError(response);
            if (response == null || response.Result == null || string.IsNullOrEmpty(response.Result.ToString()))
                return default(T);
            return response.GetResult<T>();
        }

        public async Task<T> SendRequestAsync<T>(string method, string route = null, params object[] paramList)
        {
            var response = await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(new EdjCase.JsonRpc.Core.RpcRequest(Configuration.DefaultRequestId, method, paramList)).ConfigureAwait(false);
            HandleRpcError(response);
            if (response == null || response.Result == null || string.IsNullOrEmpty(response.Result.ToString()))
                return default(T);
            return response.GetResult<T>();
        }

        public async Task SendRequestAsync(RpcRequest request, string route = null)
        {
            var response = await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(new EdjCase.JsonRpc.Core.RpcRequest(request.Id, request.Method, request.RawParameters)).ConfigureAwait(false);
            HandleRpcError(response);
        }

        public async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var response = await SendAsync<EdjCase.JsonRpc.Core.RpcRequest, RpcResponse>(new EdjCase.JsonRpc.Core.RpcRequest(Configuration.DefaultRequestId, method, paramList)).ConfigureAwait(false);
            HandleRpcError(response);
        }

        private async Task<byte[]> ReadResponseStream(Socket pipeClientStream)
        {
            var buffer = new byte[1024];
            var bufferSegment = new ArraySegment<byte>(buffer, 0, buffer.Length);

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
                            read = await pipeClientStream.ReceiveAsync(bufferSegment, SocketFlags.None).ConfigureAwait(false);
                        }
                    ).Wait(10000))
                    {
                        ms.Write(bufferSegment.Array, 0, read);
                        if (read < 1024)
                            return ms.ToArray();
                    }
                    else
                    {
                        return ms.ToArray();
                    }
                }
            }
        }

        private void HandleRpcError(RpcResponse response)
        {
            if (response != null && response.HasError)
            {
                throw new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message, response.Error.Data));
            }
        }

        private async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request)
        {
            using (var socket = new Socket(AddressFamily.Unix, SocketType.Stream, ProtocolType.IP))
            {
                if (!socket.Connected)
                {
                    try
                    {
                        await socket.ConnectAsync(_unixEndPoint).ConfigureAwait(false);
                    }
                    catch (System.Exception connEx)
                    {
                        throw new RpcConnectionNotAvailableException("IPC connection not available !!!", connEx);
                    }
                }

                try
                {
                    var rpcRequestJson = JsonConvert.SerializeObject(request, _jsonSerializationSettings);

                    var requestBytes = Encoding.UTF8.GetBytes(rpcRequestJson);
                    var requestSegment = new ArraySegment<byte>(requestBytes, 0, requestBytes.Length);

                    await socket.SendAsync(requestSegment, SocketFlags.None).ConfigureAwait(false);

                    var responseBytes = await ReadResponseStream(socket).ConfigureAwait(false);

                    if (responseBytes == null)
                    {
                        throw new RpcClientUnknownException("Invalid response / null");
                    }

                    var totalMegs = responseBytes.Length / 1024f / 1024f;

                    if (totalMegs > 10)
                    {
                        throw new RpcClientUnknownException("Response exceeds 10MB it will be impossible to parse it");
                    }

                    var responseJson = Encoding.UTF8.GetString(responseBytes);

                    try
                    {
                        var result = JsonConvert.DeserializeObject<TResponse>(responseJson, _jsonSerializationSettings);
                        if (result == null)
                        {
                            Console.WriteLine($"{DateTime.UtcNow} Null response object. RAW request:{rpcRequestJson}{Environment.NewLine}RAW response:{responseJson}");
                        }
                        return result;
                    }
                    catch (JsonSerializationException)
                    {
                        var rpcResponse = JsonConvert.DeserializeObject<RpcResponse>(responseJson, _jsonSerializationSettings);
                        if (rpcResponse == null)
                        {
                            throw new RpcClientUnknownException($"Unable to parse response from the ipc server. Response Json: {responseJson}");
                        }

                        throw rpcResponse.Error.CreateException();
                    }
                    catch (NullReferenceException)
                    {
                        throw new RpcClientUnknownException($"Unable to parse response from the ipc server. Response Json: {responseJson}");
                        //throw;
                    }
                }
                finally
                {
                    socket.Shutdown(SocketShutdown.Both);
                }
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).          
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~UnixDomainSocketClient() {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        void IDisposable.Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
