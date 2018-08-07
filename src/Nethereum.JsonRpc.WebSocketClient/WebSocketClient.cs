using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using RpcError = Nethereum.JsonRpc.Client.RpcError;

namespace Nethereum.JsonRpc.WebSocketClient
{
    public class WebSocketClient : ClientBase, IDisposable
    {
        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        protected readonly string Path;
        public static int ForceCompleteReadTotalMilliseconds { get; set; } = 2000;

        private WebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this.Path = path;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }

        protected override async Task<T> SendInnerRequestAync<T>(RpcRequest request, string route = null)
        {
            var response =
                await SendAsync<RpcRequestMessage, RpcResponseMessage>(
                        new RpcRequestMessage(request.Id, request.Method, request.RawParameters))
                    .ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        protected override async Task<T> SendInnerRequestAync<T>(string method, string route = null,
            params object[] paramList)
        {
            var response =
                await SendAsync<RpcRequestMessage, RpcResponseMessage>(
                        new RpcRequestMessage(Configuration.DefaultRequestId, method, paramList))
                    .ConfigureAwait(false);
            HandleRpcError(response);
            return response.GetResult<T>();
        }

        private void HandleRpcError(RpcResponseMessage response)
        {
            if (response.HasError)
                throw new RpcResponseException(new RpcError(response.Error.Code, response.Error.Message,
                    response.Error.Data));
        }

        public override async Task SendRequestAsync(RpcRequest request, string route = null)
        {
            var response =
                await SendAsync<RpcRequestMessage, RpcResponseMessage>(
                        new RpcRequestMessage(request.Id, request.Method, request.RawParameters))
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }

        public override async Task SendRequestAsync(string method, string route = null, params object[] paramList)
        {
            var response =
                await SendAsync<RpcRequestMessage, RpcResponseMessage>(
                        new RpcRequestMessage(Configuration.DefaultRequestId, method, paramList))
                    .ConfigureAwait(false);
            HandleRpcError(response);
        }

        private readonly object _lockingObject = new object();
        private readonly ILog _log;

        private ClientWebSocket _clientWebSocket;


        public WebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : this(path, jsonSerializerSettings)
        {
            _log = log;
        }

        private ClientWebSocket GetClientWebSocket()
        {
            try
            {
                if (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
                {
                    _clientWebSocket = new ClientWebSocket();
                    _clientWebSocket.ConnectAsync(new Uri(Path), new CancellationTokenSource(ConnectionTimeout).Token);
   
                }
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout afer {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch
            {
                //Connection error we want to allow to retry.
                _clientWebSocket = null;
                throw;
            }
            return _clientWebSocket;
        }


        public async Task<int> ReceiveBufferedResponseAsync(ClientWebSocket client, byte[] buffer)
        {
            try
            {
                var segmentBuffer = new ArraySegment<byte>(buffer);
                var result = await client
                    .ReceiveAsync(segmentBuffer, new CancellationTokenSource(ForceCompleteReadTotalMilliseconds).Token)
                    .ConfigureAwait(false);
                return result.Count;
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout afer {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
        }

        public async Task<MemoryStream> ReceiveFullResponseAsync(ClientWebSocket client)
        {
            var readBufferSize = 512;
            var memoryStream = new MemoryStream();

            int bytesRead = 0;
            byte[] buffer = new byte[readBufferSize];
            bytesRead = await ReceiveBufferedResponseAsync(client, buffer).ConfigureAwait(false);
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
                    bytesRead = await ReceiveBufferedResponseAsync(client, buffer).ConfigureAwait(false);
                }
            }
            return memoryStream;
        }

        protected async Task<TResponse> SendAsync<TRequest, TResponse>(TRequest request) where TResponse: RpcResponseMessage
        {
            var logger = new RpcLogger(_log);
            try
            {
                await semaphoreSlim.WaitAsync().ConfigureAwait(false);
                var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                var requestBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rpcRequestJson));
                logger.LogRequest(rpcRequestJson);
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                var webSocket = GetClientWebSocket();
                await webSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                using (var memoryData = await ReceiveFullResponseAsync(webSocket).ConfigureAwait(false))
                {
                    memoryData.Position = 0;
                    using (var streamReader = new StreamReader(memoryData))
                    using (var reader = new JsonTextReader(streamReader))
                    {
                        var serializer = JsonSerializer.Create(JsonSerializerSettings);
                        var message = serializer.Deserialize<TResponse>(reader);
                        logger.LogResponse(message);
                        return message;
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw new RpcClientUnknownException("Error occurred when trying to send ipc requests(s)", ex);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public void Dispose()
        {
            _clientWebSocket?.Dispose();
        }
    }
}

