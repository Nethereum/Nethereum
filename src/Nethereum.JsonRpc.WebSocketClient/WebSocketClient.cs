
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
#if NETSTANDARD2_0_OR_GREATER || NETCOREAPP3_1_OR_GREATER || NET461_OR_GREATER || NET5_0_OR_GREATER
using Microsoft.Extensions.Logging;
#endif

namespace Nethereum.JsonRpc.WebSocketClient
{
    public class WebSocketClient : ClientBase, IDisposable, IClientRequestHeaderSupport
    {
        private readonly SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
        protected string Path { get; set; }
        public static int ForceCompleteReadTotalMilliseconds { get; set; } = 2000;

        private WebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null)
        {
            this.SetBasicAuthenticationHeaderFromUri(new Uri(path));
            if (jsonSerializerSettings == null)
            {
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            }

            Path = path;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        private readonly object _lockingObject = new object();
        private readonly ILogger _log;

        private ClientWebSocket _clientWebSocket;


        public WebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null, ILogger log = null) : this(path, jsonSerializerSettings)
        {
            _log = log;
        }

        public  Task StopAsync()
        {
             return StopAsync(WebSocketCloseStatus.NormalClosure, "", new CancellationTokenSource(ConnectionTimeout).Token);
        }

        public async Task StopAsync(WebSocketCloseStatus webSocketCloseStatus, string status, CancellationToken timeOutToken)
        {
            try
            {
                if (_clientWebSocket != null && (_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.Connecting))
                {

                    await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                    await _clientWebSocket.CloseOutputAsync(webSocketCloseStatus, status, timeOutToken).ConfigureAwait(false);
                    while (_clientWebSocket.State != WebSocketState.Closed && !timeOutToken.IsCancellationRequested) ;
                }
                
            }
            finally
            {
                _semaphoreSlim.Release();
            }

        }

        private async Task<ClientWebSocket> GetClientWebSocketAsync()
        {
            try
            {
                if (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
                {
                    _clientWebSocket = new ClientWebSocket();
                    if (RequestHeaders != null)
                    {
                        foreach (var requestHeader in RequestHeaders)
                        {
                            _clientWebSocket.Options.SetRequestHeader(requestHeader.Key, requestHeader.Value);
                        }
                    }
                    await _clientWebSocket.ConnectAsync(new Uri(Path), new CancellationTokenSource(ConnectionTimeout).Token).ConfigureAwait(false);

                }
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
            catch
            {
                //Connection error we want to allow to retry.
                _clientWebSocket.Dispose();
                _clientWebSocket = null;
                throw;
            }
            return _clientWebSocket;
        }


        public async Task<WebSocketReceiveResult> ReceiveBufferedResponseAsync(ClientWebSocket client, byte[] buffer)
        {
            try
            {
                var segmentBuffer = new ArraySegment<byte>(buffer);
                return await client
                    .ReceiveAsync(segmentBuffer, new CancellationTokenSource(ForceCompleteReadTotalMilliseconds).Token)
                    .ConfigureAwait(false);
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ForceCompleteReadTotalMilliseconds} milliseconds", ex);
            }
        }

        public async Task<MemoryStream> ReceiveFullResponseAsync(ClientWebSocket client)
        {
            var readBufferSize = 512;
            var memoryStream = new MemoryStream();

            var buffer = new byte[readBufferSize];
            var completedMessage = false;

            while (!completedMessage)
            {
                var receivedResult = await ReceiveBufferedResponseAsync(client, buffer).ConfigureAwait(false);
                var bytesRead = receivedResult.Count;
                if (bytesRead > 0)
                {
                    memoryStream.Write(buffer, 0, bytesRead);
                    var lastByte = buffer[bytesRead - 1];

                    if (lastByte == 10 || receivedResult.EndOfMessage)  //return signaled with a line feed / or just less than the full message
                    {
                        completedMessage = true;
                    }
                }
                else
                {
                    // We have had a response already and EndOfMessage
                    if(receivedResult.EndOfMessage)
                    {
                        completedMessage = true;
                    }
                }
            }

            if (memoryStream.Length == 0) return await ReceiveFullResponseAsync(client).ConfigureAwait(false); //empty response
            return memoryStream;
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
        {
            var logger = new RpcLogger(_log);
            try
            {
                await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                var requestBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rpcRequestJson));
                logger.LogRequest(rpcRequestJson);
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                var webSocket = await GetClientWebSocketAsync().ConfigureAwait(false);
                await webSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                using (var memoryData = await ReceiveFullResponseAsync(webSocket).ConfigureAwait(false))
                {
                    memoryData.Position = 0;
                    using (var streamReader = new StreamReader(memoryData))
                    using (var reader = new JsonTextReader(streamReader))
                    {
                        var serializer = JsonSerializer.Create(JsonSerializerSettings);
                        var message = serializer.Deserialize<RpcResponseMessage>(reader);
                        logger.LogResponse(message);
                        return message;
                    }
                }
            }
            catch (Exception ex)
            {
                var exception = new RpcClientUnknownException("Error occurred when trying to web socket requests(s): " + request.Method, ex);
                logger.LogException(exception);
                throw exception;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        protected async override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
        {
            var logger = new RpcLogger(_log);
            try
            {
                await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                var rpcRequestJson = JsonConvert.SerializeObject(requests, JsonSerializerSettings);
                var requestBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rpcRequestJson));
                logger.LogRequest(rpcRequestJson);
                var cancellationTokenSource = new CancellationTokenSource();
                cancellationTokenSource.CancelAfter(ConnectionTimeout);

                var webSocket = await GetClientWebSocketAsync().ConfigureAwait(false);
                await webSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, cancellationTokenSource.Token)
                    .ConfigureAwait(false);

                using (var memoryData = await ReceiveFullResponseAsync(webSocket).ConfigureAwait(false))
                {
                    memoryData.Position = 0;
                    using (var streamReader = new StreamReader(memoryData))
                    using (var reader = new JsonTextReader(streamReader))
                    {
                        var serializer = JsonSerializer.Create(JsonSerializerSettings);
                        var messages = serializer.Deserialize<RpcResponseMessage[]>(reader);
                        return messages;
                    }
                }
            }
            catch (Exception ex)
            {
                var exception = new RpcClientUnknownException("Error occurred when trying to web socket requests(s)", ex);
                logger.LogException(exception);
                throw exception;
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public void Dispose()
        {
            try
            {
                StopAsync().Wait();
            }
            catch 
            {
                
            }
            _clientWebSocket?.Dispose();
            _clientWebSocket = null;
        }
    }
}

