using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    /// <summary>
    /// TODO: 
    /// * Interceptor support and other generic stuff, interceptors need response handler support?
    /// </summary>
    ///
    public delegate void WebSocketStreamingErrorEventHandler(object sender, Exception ex);

    public class StreamingWebSocketClient : IStreamingClient, IDisposable
    {
        public static TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20.0);

        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);

        private readonly string _path;
        public static int ForceCompleteReadTotalMilliseconds { get; set; } = 100000;

        private ConcurrentDictionary<string, IRpcStreamingResponseHandler> _requests = new ConcurrentDictionary<string, IRpcStreamingResponseHandler>();

        private Task _listener;
        private CancellationTokenSource _cancellationTokenSource;

        public event WebSocketStreamingErrorEventHandler Error;

        private StreamingWebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this._path = path;
            JsonSerializerSettings = jsonSerializerSettings;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        private readonly object _lockingObject = new object();
        private readonly ILog _log;

        public bool IsStarted { get; private set; }

        private ClientWebSocket _clientWebSocket;

        private bool AnyQueueRequests()
        {
            return !_requests.IsEmpty;
        }

        private void HandleRequest(RpcRequestMessage request, IRpcStreamingResponseHandler requestResponseHandler)
        {
            _requests.TryAdd(request.Id.ToString(), requestResponseHandler);
        }

        public bool AddSubscription(string subscriptionId, IRpcStreamingResponseHandler handler)
        {
            return _requests.TryAdd(subscriptionId, handler);
        }

        public bool RemoveSubscription(string subscriptionId)
        {
            return _requests.TryRemove(subscriptionId, out var handler);
        }

        private void HandleResponse(RpcStreamingResponseMessage response)
        {
            if (response.Method != null) // subscription
            {
                IRpcStreamingResponseHandler handler;

                if (_requests.TryGetValue(response.Params.Subscription, out handler))
                {
                    handler.HandleResponse(response);
                }
            }
            else
            {
                IRpcStreamingResponseHandler handler;

                if (_requests.TryRemove(response.Id.ToString(), out handler))
                {
                     handler.HandleResponse(response);
                }
            }
        }

        public StreamingWebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : this(path, jsonSerializerSettings)
        {
            _log = log;
        }

        public Task StartAsync()

        {
            if (IsStarted)
            {
                return Task.CompletedTask;
            }

            _cancellationTokenSource = new CancellationTokenSource();
 
            _listener = Task.Factory.StartNew(async () =>
            {
                await HandleIncomingMessagesAsync();
            }, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);

            IsStarted = true;

            return ConnectWebSocketAsync();

        }

        public Task StopAsync()
        {
            return CloseDisposeAndClearRequestsAsync();
        }

        private async Task ConnectWebSocketAsync()
        {
            try
            {
                if (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
                {
                    _clientWebSocket = new ClientWebSocket();
                    await _clientWebSocket.ConnectAsync(new Uri(_path), new CancellationTokenSource(ConnectionTimeout).Token).ConfigureAwait(false);

                }
            }
            catch (TaskCanceledException ex)
            {
                var rpcException = new RpcClientTimeoutException($"Websocket connection timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
                HandleError(rpcException);
                throw rpcException;
            }
            catch(Exception ex)
            {
                HandleError(ex);
                throw;
            }
        }

        private void HandleError(Exception exception)
        {
            Error?.Invoke(this,exception);
            foreach (var rpcStreamingResponseHandler in _requests)
            {
                rpcStreamingResponseHandler.Value.HandleClientError(exception);    
            }
            CloseDisposeAndClearRequestsAsync().Wait();
        }

        public async Task<int> ReceiveBufferedResponseAsync(ClientWebSocket client, byte[] buffer)
        {
            try
            {
                var timeoutToken = new CancellationTokenSource(ForceCompleteReadTotalMilliseconds).Token;
                var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, timeoutToken);

                if (client == null) return 0;
                var segmentBuffer = new ArraySegment<byte>(buffer);
                var result = await client
                    .ReceiveAsync(segmentBuffer, tokenSource.Token)
                    .ConfigureAwait(false);
                return result.Count;
            }
            catch (TaskCanceledException ex)
            {
                var exception = new RpcClientTimeoutException($"Rpc timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
                HandleError(exception);
                throw exception;
            }
        }

        private byte[] lastBuffer;

        //this could be moved to AsycEnumerator
        public async Task<MemoryStream> ReceiveFullResponseAsync(ClientWebSocket client)
        {
            var readBufferSize = 512;
            var memoryStream = new MemoryStream();
            bool completedNextMessage = false;


            while (!completedNextMessage && !_cancellationTokenSource.IsCancellationRequested)
            {
                if (lastBuffer != null && lastBuffer.Length > 0)
                {
                    completedNextMessage = ProcessNextMessageBytes(lastBuffer, memoryStream);
                }
                else
                {
                    var buffer = new byte[readBufferSize];
                    var bytesRead = await ReceiveBufferedResponseAsync(client, buffer).ConfigureAwait(false);
                    if (bytesRead == 0) completedNextMessage = true;
                   
                    completedNextMessage = ProcessNextMessageBytes(buffer, memoryStream, bytesRead);
                }
            }

            return memoryStream;
        }

        protected bool ProcessNextMessageBytes(byte[] buffer, MemoryStream memoryStream, int bytesRead=-1)
        {
            int currentIndex = 0;
            //bytesRead == -1 used to signal cached previous buffer
            bool finishedReading = bytesRead != -1 && bytesRead != buffer.Length;
            int bufferToRead = buffer.Length;

            if (finishedReading)
            {
                bufferToRead = bytesRead;
            }

            while(currentIndex < bufferToRead)
            {
                if(buffer[currentIndex] == 10)
                {
                    if (currentIndex + 1 < bufferToRead) // bytes remaining
                    {
                        lastBuffer = new byte[bytesRead - currentIndex];
                        Array.Copy(buffer, currentIndex + 1, lastBuffer, 0, bytesRead - currentIndex);
                    }
                    else
                    {
                        lastBuffer = null;
                    }
                    currentIndex = buffer.Length;
                    return true;
                }
                else
                {
                    memoryStream.WriteByte(buffer[currentIndex]);
                    currentIndex = currentIndex + 1;
                }
            }

            if (finishedReading)
            {
                return true;
            }

            return false;
        }
            

        private async Task HandleIncomingMessagesAsync()
        {
            var logger = new RpcLogger(_log);
            while (!_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open && AnyQueueRequests())
                    {
                        using (var memoryData = await ReceiveFullResponseAsync(_clientWebSocket).ConfigureAwait(false))
                        {
                            if (memoryData.Length == 0) return;
                            memoryData.Position = 0;
                            using (var streamReader = new StreamReader(memoryData))
                            using (var reader = new JsonTextReader(streamReader))
                            {
                                var serializer = JsonSerializer.Create(JsonSerializerSettings);
                                var message = serializer.Deserialize<RpcStreamingResponseMessage>(reader);
                                HandleResponse(message);
                                logger.LogResponse(message);

                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    HandleError(ex);
                    logger.LogException(ex);
                }
            }
        }

        public async Task SendRequestAsync(RpcRequestMessage request, IRpcStreamingResponseHandler requestResponseHandler, string route = null )
        {
            if (_clientWebSocket == null) throw new InvalidOperationException("Websocket is null.  Ensure that StartAsync has been called to create the websocket.");

            var logger = new RpcLogger(_log);
            
            try
            {
                await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                var requestBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rpcRequestJson));
                logger.LogRequest(rpcRequestJson);
                var timeoutCancellationTokenSource = new CancellationTokenSource();
                timeoutCancellationTokenSource.CancelAfter(ConnectionTimeout);

                var webSocket = _clientWebSocket;
                await webSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, timeoutCancellationTokenSource.Token)
                    .ConfigureAwait(false);
                HandleRequest(request, requestResponseHandler);

            }
            catch (Exception ex)
            {
                logger.LogException(ex);
                throw new RpcClientUnknownException("Error occurred trying to send web socket requests(s)", ex);
            }
            finally
            {
                _semaphoreSlim.Release();
            }
        }

        public void Dispose()
        {
            CloseDisposeAndClearRequestsAsync().Wait();
        }

        private async Task CloseDisposeAndClearRequestsAsync()
        {
            IsStarted = false;

            try
            {
                if (_clientWebSocket != null)
                {
                    await _clientWebSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "",
                        new CancellationTokenSource(ConnectionTimeout).Token);
                }

                _clientWebSocket?.Dispose();

            }
            catch
            {

            }
            finally
            {
                _clientWebSocket = null;
            }

            try
            {
                _cancellationTokenSource?.Cancel();
                _cancellationTokenSource?.Dispose();
            }
            catch
            {

            }
            finally
            {
                _cancellationTokenSource = null;
            }

#if !NETSTANDARD1_3
            try
            {
                _listener?.Dispose();
            }
            catch
            {

            }
            finally
            {
                _listener = null;
            }
#else
            _listener = null;

#endif
            try
            {
                foreach (var rpcStreamingResponseHandler in _requests)
                {
                    rpcStreamingResponseHandler.Value.HandleClientDisconnection();
                }
            }
            catch
            {

            }
            finally
            {
                _requests.Clear();
            }

        }

        public async Task SendRequestAsync(RpcRequest request, IRpcStreamingResponseHandler requestResponseHandler, string route = null)
        {
            var reqMsg = new RpcRequestMessage(request.Id,
                                             request.Method,
                                             request.RawParameters);
             await SendRequestAsync(reqMsg, requestResponseHandler, route).ConfigureAwait(false);
        }
    }
}
