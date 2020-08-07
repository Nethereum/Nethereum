using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.JsonRpc.WebSocketClient
{
    public class WebSocketClient : ClientBase, IDisposable
    {
        private readonly SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);
        protected string Path { get; set; }
        public static int ForceCompleteReadTotalMilliseconds { get; set; } = 2000;

        private WebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
            {
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            }

            Path = path;
            JsonSerializerSettings = jsonSerializerSettings;
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        private readonly object _lockingObject = new object();
        private readonly ILog _log;

        private ClientWebSocket _clientWebSocket;


        public WebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : this(path, jsonSerializerSettings)
        {
            _log = log;
        }

        private async Task<ClientWebSocket> GetClientWebSocketAsync()
        {
            try
            {
                if (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
                {
                    _clientWebSocket = new ClientWebSocket();
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
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
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

            if (memoryStream.Length == 0) return await ReceiveFullResponseAsync(client); //empty response
            return memoryStream;
        }

        protected override async Task<RpcResponseMessage> SendAsync(RpcRequestMessage request, string route = null)
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
                var exception = new RpcClientUnknownException("Error occurred when trying to web socket requests(s)", ex);
                logger.LogException(exception);
                throw exception;
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

