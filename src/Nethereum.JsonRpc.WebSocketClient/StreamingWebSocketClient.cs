using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.WebSockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;

namespace Nethereum.JsonRpc.WebSocketClient
{
    public class StreamingWebSocketClient : StreamingClientBase, IDisposable
    {
        private SemaphoreSlim semaphoreSlim = new SemaphoreSlim(1, 1);

        protected readonly string Path;
        public static int ForceCompleteReadTotalMilliseconds { get; set; } = 2000;

        private Task listener;
        private CancellationTokenSource cancellationTokenSource;

        private StreamingWebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this.Path = path;
            JsonSerializerSettings = jsonSerializerSettings;
            cancellationTokenSource = new CancellationTokenSource();            
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        private readonly object _lockingObject = new object();
        private readonly ILog _log;

        private ClientWebSocket _clientWebSocket;

        public StreamingWebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : this(path, jsonSerializerSettings)
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

        private async Task<int> ReceiveBufferedResponseAsync(ClientWebSocket client, byte[] buffer, CancellationToken cancellationToken)
        {
            try
            {
                var timeoutToken = new CancellationTokenSource(ForceCompleteReadTotalMilliseconds).Token;
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeoutToken);

                var segmentBuffer = new ArraySegment<byte>(buffer);
                var result = await client
                    .ReceiveAsync(segmentBuffer, tokenSource.Token)
                    .ConfigureAwait(false);
                return result.Count;
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
            }
        }

        private async Task HandleIncomingMessagesAsync(ClientWebSocket client, CancellationToken cancellationToken)
        {
            var lastChunk = string.Empty;
            var readBufferSize = 512;

            int bytesRead = 0;
            byte[] chunkedBuffer = new byte[readBufferSize];
            bytesRead = await ReceiveBufferedResponseAsync(client, chunkedBuffer, cancellationToken).ConfigureAwait(false);
            while (!cancellationToken.IsCancellationRequested && bytesRead > 0)
            {
                var data = Encoding.UTF8.GetString(chunkedBuffer, 0, bytesRead);
                var dechunkedData = DeChunkResponse(data);

                foreach (var chunk in dechunkedData)
                {
                    var localChunk = chunk;
                    if (!string.IsNullOrEmpty(lastChunk))
                    {
                        localChunk = lastChunk + localChunk;
                    }

                    try
                    {
                        var temp = JsonConvert.DeserializeAnonymousType(localChunk, new { id = string.Empty }, JsonSerializerSettings);

                        if (temp.id == null)
                        {
                            // assume streaming subscription response
                            RpcStreamingResponseMessage streamingResult = JsonConvert.DeserializeObject<RpcStreamingResponseMessage>(localChunk, JsonSerializerSettings);
                            var streamingArgs = new RpcStreamingResponseMessageEventArgs(streamingResult);

                            OnStreamingMessageRecieved(this, streamingArgs);
                        }
                        else
                        {
                            // assume regular rpc response
                            RpcResponseMessage result = JsonConvert.DeserializeObject<RpcResponseMessage>(localChunk, JsonSerializerSettings);
                            var rpcEventArgs = new RpcResponseMessageEventArgs(result);

                            OnMessageRecieved(this, rpcEventArgs);

                            continue;
                        }

                        lastChunk = string.Empty;
                    }
                    catch (Exception)
                    {
                        lastChunk = localChunk;
                        // swallow...
                        continue;
                    }
                }

                bytesRead = await ReceiveBufferedResponseAsync(client, chunkedBuffer, cancellationToken).ConfigureAwait(false);
            }
        }

        protected override async Task SendAsync(RpcRequestMessage request, string route = null)
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

                if (listener != null)
                {
                    cancellationTokenSource.Cancel();
                    cancellationTokenSource.Dispose();
                    cancellationTokenSource = new CancellationTokenSource();
                }

                listener = Task.Factory.StartNew(async () =>
                {
                    await HandleIncomingMessagesAsync(_clientWebSocket, CancellationToken.None);
                }, CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Current);
            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw new RpcClientUnknownException("Error occurred trying to send web socket requests(s)", ex);
            }
            finally
            {
                semaphoreSlim.Release();
            }
        }

        public void Dispose()
        {
            _clientWebSocket?.Dispose();
            cancellationTokenSource.Dispose();
        }

        private IEnumerable<string> DeChunkResponse(string response)
        {
            var dechunked = Regex.Replace(response, @"\}[\n\r]?\{", "}|--|{");
            dechunked = Regex.Replace(dechunked, @"\}\][\n\r]?\[\{", "}]|--|[{");
            dechunked = Regex.Replace(dechunked, @"\}[\n\r]?\[\{", "}|--|[{");
            dechunked = Regex.Replace(dechunked, @"\}\][\n\r]?\{", "}]|--|{");
            return dechunked.Split(new[] { "|--|" }, StringSplitOptions.RemoveEmptyEntries);
        }
    }
}

