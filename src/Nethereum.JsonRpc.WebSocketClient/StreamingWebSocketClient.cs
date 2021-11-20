using Common.Logging;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.Streaming;

namespace Nethereum.JsonRpc.WebSocketStreamingClient
{
    public delegate void WebSocketStreamingErrorEventHandler(object sender, Exception ex);

    public class StreamingWebSocketClient : IStreamingClient, IDisposable, IClientRequestHeaderSupport
    {
        public Dictionary<string, string> RequestHeaders { get; set; } = new Dictionary<string, string>();
        public static TimeSpan ConnectionTimeout { get; set; } = TimeSpan.FromSeconds(20.0);
        public static int ForceCompleteReadTotalMilliseconds { get; set; } = 100000;
        public JsonSerializerSettings JsonSerializerSettings { get; set; }
        public bool IsStarted { get; private set; }
        public event WebSocketStreamingErrorEventHandler Error;

        private SemaphoreSlim _sendRequestSemaphore = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _startStopSemaphore = new SemaphoreSlim(1, 1);

        private readonly string _path;
        private ConcurrentDictionary<string, IRpcStreamingResponseHandler> _requests = new ConcurrentDictionary<string, IRpcStreamingResponseHandler>();
        private Task _listener;
        private CancellationTokenSource _cancellationTokenSource;
        private readonly ILog _log;
        private ClientWebSocket _clientWebSocket;

        private StreamingWebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null)
        {
            if (jsonSerializerSettings == null)
                jsonSerializerSettings = DefaultJsonSerializerSettingsFactory.BuildDefaultJsonSerializerSettings();
            this._path = path;
            this.SetBasicAuthenticationHeaderFromUri(new Uri(path));
            JsonSerializerSettings = jsonSerializerSettings;
            _cancellationTokenSource = new CancellationTokenSource();
        }

        public StreamingWebSocketClient(string path, JsonSerializerSettings jsonSerializerSettings = null, ILog log = null) : this(path, jsonSerializerSettings)
        {
            _log = log;
        }

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

        public async Task StartAsync()
        {
            await _startStopSemaphore.WaitAsync();
            if (IsStarted)
            {
                _startStopSemaphore.Release();
                return;
            }
            IsStarted = true;

            _cancellationTokenSource = new CancellationTokenSource();
            _clientWebSocket = new ClientWebSocket();

            try
            {
                using (var tokenSource = new CancellationTokenSource(ConnectionTimeout))
                {
                    if (RequestHeaders != null)
                    {
                        foreach (var requestHeader in RequestHeaders)
                        {
                            _clientWebSocket.Options.SetRequestHeader(requestHeader.Key, requestHeader.Value);
                        }
                    }

                    await _clientWebSocket.ConnectAsync(new Uri(_path), tokenSource.Token).ConfigureAwait(false);
                }

                //Listener must be started after the _clientWebSocket is connected. Check #738 for more info.
                _listener = Task.Factory.StartNew(async () =>
                {
                    await HandleIncomingMessagesAsync();
                }, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current).Unwrap();
            }
            catch (TaskCanceledException ex)
            {
                var rpcException = new RpcClientTimeoutException($"Websocket connection timeout after {ConnectionTimeout.TotalMilliseconds} milliseconds", ex);
                HandleError(rpcException, false);
                throw rpcException;
            }
            catch (Exception ex)
            {
                HandleError(ex, false);
                throw;
            }
            finally
            {
                _startStopSemaphore.Release();
            }
        }

        private void HandleError(Exception exception, bool useStartStopSemaphore = true)
        {
            foreach (var rpcStreamingResponseHandler in _requests)
            {
                rpcStreamingResponseHandler.Value.HandleClientError(exception);
            }
            CloseDisposeAndClearRequestsAsync(useStartStopSemaphore).Wait();
            Error?.Invoke(this, exception);
        }

        public async Task<Tuple<int, bool>> ReceiveBufferedResponseAsync(ClientWebSocket client, byte[] buffer)
        {
            try
            {
                using (var timeoutTokenSource = new CancellationTokenSource(ForceCompleteReadTotalMilliseconds))
                {
                    using (var tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, timeoutTokenSource.Token))
                    {
                        if (client == null) return new Tuple<int, bool>(0, true);
                        var segmentBuffer = new ArraySegment<byte>(buffer);
                        var result = await client
                            .ReceiveAsync(segmentBuffer, tokenSource.Token)
                            .ConfigureAwait(false);
                        return new Tuple<int, bool>(result.Count, result.EndOfMessage);
                    }
                }
            }
            catch (TaskCanceledException ex)
            {
                if (!_cancellationTokenSource.IsCancellationRequested)
                {
                    throw new RpcClientTimeoutException($"Rpc timeout after {ForceCompleteReadTotalMilliseconds} milliseconds", ex);
                }
                else
                {
                    throw;
                }
            }
        }

        private byte[] _lastBuffer;

        //this could be moved to AsycEnumerator
        public async Task<MemoryStream> ReceiveFullResponseAsync(ClientWebSocket client)
        {
            var readBufferSize = 512;
            var memoryStream = new MemoryStream();
            bool completedNextMessage = false;

            while (!completedNextMessage && !_cancellationTokenSource.IsCancellationRequested)
            {
                if (_lastBuffer != null && _lastBuffer.Length > 0)
                {
                    completedNextMessage = ProcessNextMessageBytes(_lastBuffer, memoryStream);
                }
                else
                {
                    var buffer = new byte[readBufferSize];
                    var response = await ReceiveBufferedResponseAsync(client, buffer).ConfigureAwait(false);

                    completedNextMessage = ProcessNextMessageBytes(buffer, memoryStream, response.Item1, response.Item2);
                }
            }

            return memoryStream;
        }

        protected bool ProcessNextMessageBytes(byte[] buffer, MemoryStream memoryStream, int bytesRead = -1, bool endOfMessage = false)
        {
            int currentIndex = 0;
            //if bytes read is 0 we don't care as streaming might continue regardless and message split.. 
            int bufferToRead = bytesRead;
            if (bytesRead == -1) bufferToRead = buffer.Length; //old data from previous buffer

            while (currentIndex < bufferToRead)
            {
                if (buffer[currentIndex] == 10) //finish reading message
                {
                    if (currentIndex + 1 < bufferToRead) // bytes remaining for next message add them to last buffer to be read next message.
                    {
                        _lastBuffer = new byte[bytesRead - currentIndex];
                        Array.Copy(buffer, currentIndex + 1, _lastBuffer, 0, bytesRead - currentIndex);
                    }
                    else
                    {
                        _lastBuffer = null;
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

            if (endOfMessage) // somehow the ethereum end of message was not signaled use the end of message of the websocket
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
                    if (AnyQueueRequests())
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
                    else
                    {
                        await Task.Yield();
                    }
                }
                catch (Exception ex)
                {
                    //HandleError should not be called from any of the functions that are called from this function 
                    //which also rethrow the exception to avoid calling the error handlers twice for the same error.
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                        //This task must not wait for the HandleError as it will wait for this task as well
                        _ = Task.Run(() =>
                        {
                            HandleError(ex);
                            logger.LogException(ex);
                        });
                    }
                }
            }
        }

        public async Task SendRequestAsync(RpcRequestMessage request, IRpcStreamingResponseHandler requestResponseHandler, string route = null)
        {
            if (_clientWebSocket == null) throw new InvalidOperationException("Websocket is null. Ensure that StartAsync has been called to create the websocket.");

            var logger = new RpcLogger(_log);
            using (var timeoutCancellationTokenSource = new CancellationTokenSource())
            {
                try
                {
                    await _sendRequestSemaphore.WaitAsync().ConfigureAwait(false);
                    var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                    var requestBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rpcRequestJson));
                    logger.LogRequest(rpcRequestJson);
                    timeoutCancellationTokenSource.CancelAfter(ConnectionTimeout);

                    await _clientWebSocket.SendAsync(requestBytes, WebSocketMessageType.Text, true, timeoutCancellationTokenSource.Token)
                        .ConfigureAwait(false);
                    HandleRequest(request, requestResponseHandler);
                }
                catch (Exception ex)
                {
                    throw new RpcClientUnknownException("Error occurred trying to send web socket requests(s)", ex);
                }
                finally
                {
                    _sendRequestSemaphore.Release();
                }
            }
        }

        public void Dispose()
        {
            CloseDisposeAndClearRequestsAsync().Wait();
        }

        public Task StopAsync()
        {
            return CloseDisposeAndClearRequestsAsync();
        }

        private async Task CloseDisposeAndClearRequestsAsync(bool waitForSemaphore = true)
        {
            if (waitForSemaphore)
            {
                await _startStopSemaphore.WaitAsync();
            }
            if (!IsStarted)
            {
                if (waitForSemaphore)
                {
                    _startStopSemaphore.Release();
                }
                return;
            }
            IsStarted = false;

            //Cancel listener
            _cancellationTokenSource?.Cancel();
            try
            {
                _listener?.Wait();

                _cancellationTokenSource?.Dispose();
                _cancellationTokenSource = null;

                //Dispose listener
#if !NETSTANDARD1_3
                _listener?.Dispose();
#endif
                _listener = null;
            }
            catch { }

            //Close the clientWebSocket
            using (var tokenSource = new CancellationTokenSource(ConnectionTimeout))
            {
                try
                {
                    if (_clientWebSocket != null && (_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.Connecting))
                    {
                        await _clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "",
                            tokenSource.Token);
                    }
                }
                catch { }
            }

            _clientWebSocket?.Dispose();
            _clientWebSocket = null;

            //Let all the _requests the client is disconnecting and clear them
            try
            {
                foreach (var rpcStreamingResponseHandler in _requests)
                {
                    rpcStreamingResponseHandler.Value.HandleClientDisconnection();
                }
            }
            catch { }
            _requests.Clear();

            if (waitForSemaphore)
            {
                _startStopSemaphore.Release();
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
