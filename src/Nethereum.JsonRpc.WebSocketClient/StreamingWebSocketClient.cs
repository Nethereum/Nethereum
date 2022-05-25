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

        private SemaphoreSlim _semaphoreSlim = new SemaphoreSlim(1, 1);
        private SemaphoreSlim _semaphoreSlimListener = new SemaphoreSlim(1, 1);

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
            this.SetBasicAuthenticationHeaderFromUri(new Uri(path));
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

            IsStarted = true;

            if(_cancellationTokenSource == null)
            {
                _cancellationTokenSource = new CancellationTokenSource();
            }

            _listener = Task.Factory.StartNew(async () =>
            {
                await HandleIncomingMessagesAsync();
            }, _cancellationTokenSource.Token, TaskCreationOptions.LongRunning, TaskScheduler.Current);
           
            return ConnectWebSocketAsync();
        }

        

        private async Task ConnectWebSocketAsync()
        {
            CancellationTokenSource tokenSource = null;
            try
            {
                if (_clientWebSocket == null || _clientWebSocket.State != WebSocketState.Open)
                {
                    tokenSource = new CancellationTokenSource(ConnectionTimeout);

                    _clientWebSocket?.Dispose();
                    _clientWebSocket = new ClientWebSocket();
                    if (RequestHeaders != null)
                    {
                        foreach (var requestHeader in RequestHeaders)
                        {
                            _clientWebSocket.Options.SetRequestHeader(requestHeader.Key, requestHeader.Value);
                        }
                    }

                    await _clientWebSocket.ConnectAsync(new Uri(_path), tokenSource.Token).ConfigureAwait(false);

                    //Random random = new Random();
                    //var x = random.Next(0, 30);
                    //if (x == 5) throw new Exception("Error");

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
            finally
            {
                tokenSource?.Dispose();
            }
        }

        private void HandleError(Exception exception)
        {
            //First send errors exceptions
            foreach (var rpcStreamingResponseHandler in _requests)
            {
                rpcStreamingResponseHandler.Value.HandleClientError(exception);
            }
            //Stop websocketclient and dispose everything
            StopAsync().Wait();
            //Send event of error
            Error?.Invoke(this, exception);
            
        }

        public async Task<Tuple<int, bool>> ReceiveBufferedResponseAsync(ClientWebSocket client, byte[] buffer)
        {
            CancellationTokenSource timeoutTokenSource = null;
            CancellationTokenSource tokenSource = null;
            try
            {
                timeoutTokenSource = new CancellationTokenSource(ForceCompleteReadTotalMilliseconds);
                tokenSource = CancellationTokenSource.CreateLinkedTokenSource(_cancellationTokenSource.Token, timeoutTokenSource.Token);

                if (client == null) return new Tuple<int, bool>(0, true);
                var segmentBuffer = new ArraySegment<byte>(buffer);
                var result = await client
                    .ReceiveAsync(segmentBuffer, tokenSource.Token)
                    .ConfigureAwait(false);
                return new Tuple<int, bool>(result.Count, result.EndOfMessage);
            }
            catch (TaskCanceledException ex)
            {
                throw new RpcClientTimeoutException($"Rpc timeout after {ForceCompleteReadTotalMilliseconds} milliseconds", ex);
            }
            finally
            {
                timeoutTokenSource?.Dispose();
                tokenSource?.Dispose();
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

        protected bool ProcessNextMessageBytes(byte[] buffer, MemoryStream memoryStream, int bytesRead=-1, bool endOfMessage = false)
        {
            int currentIndex = 0;
            //if bytes read is 0 we don't care as streaming might continue regardless and message split.. 
            int bufferToRead = bytesRead;
            if (bytesRead == -1) bufferToRead = buffer.Length; //old data from previous buffer
           
            while (currentIndex < bufferToRead)
            {
                if(buffer[currentIndex] == 10 ) //finish reading message
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
            while (_cancellationTokenSource != null && !_cancellationTokenSource.IsCancellationRequested)
            {
                try
                {
                    await _semaphoreSlimListener.WaitAsync().ConfigureAwait(false);
                    if (_clientWebSocket != null && _clientWebSocket.State == WebSocketState.Open && AnyQueueRequests())
                    {
                        using (var memoryData = await ReceiveFullResponseAsync(_clientWebSocket).ConfigureAwait(false))
                        {
                            if (memoryData.Length != 0)
                            {
                                memoryData.Position = 0;
                                using (var streamReader = new StreamReader(memoryData))
                                using (var reader = new JsonTextReader(streamReader))
                                {
                                    var serializer = JsonSerializer.Create(JsonSerializerSettings);
                                    var message = serializer.Deserialize<RpcStreamingResponseMessage>(reader);
                                    //Random random = new Random();
                                    //var x = random.Next(0, 50);
                                    //if (x == 5) throw new Exception("Error");

                                    HandleResponse(message);
                                    logger.LogResponse(message);

                                }
                            }
                        }
                    }
                }
                catch (Exception ex)
                {
                    //HandleError should not be called from any of the functions that are called from this function 
                    //which also rethrow the exception to avoid calling the error handlers twice for the same error.
                    //if cancellation requested ignore it as we will end up in a loop
                    if (!_cancellationTokenSource.IsCancellationRequested)
                    {
                       
                        HandleError(ex);
                        logger.LogException(ex);
                        
                    }
                }
                finally
                {
                    //release the semaphore for the listener so it can be disposed
                    _semaphoreSlimListener.Release();
                }
            }
        }

        public async Task SendRequestAsync(RpcRequestMessage request, IRpcStreamingResponseHandler requestResponseHandler, string route = null )
        {
            if (_clientWebSocket == null) throw new InvalidOperationException("Websocket is null.  Ensure that StartAsync has been called to create the websocket.");

            var logger = new RpcLogger(_log);
            CancellationTokenSource timeoutCancellationTokenSource = null;
            try
            {
                await _semaphoreSlim.WaitAsync().ConfigureAwait(false);
                var rpcRequestJson = JsonConvert.SerializeObject(request, JsonSerializerSettings);
                var requestBytes = new ArraySegment<byte>(Encoding.UTF8.GetBytes(rpcRequestJson));
                logger.LogRequest(rpcRequestJson);
                timeoutCancellationTokenSource = new CancellationTokenSource();
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
                timeoutCancellationTokenSource?.Dispose();
                _semaphoreSlim.Release();
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

        private async Task CloseDisposeAndClearRequestsAsync()
        {
            IsStarted = false;
            //Cancel listener
            _cancellationTokenSource?.Cancel();
            try
            {
                 //We could wait but this means the websocket will be in bad state before trying to close it or dispose it
                // _listener?.Wait();
                
                //wait for the cancellation to be completed (or wait a second)
                await _semaphoreSlimListener.WaitAsync(1000).ConfigureAwait(false);
                //Dispose listener
#if !NETSTANDARD1_3
                try
                {
                    _listener?.Dispose();
                }
                catch { }
                finally
                {
                    _listener = null;
                }
#else
            _listener = null;
#endif

                try
                {
                    _cancellationTokenSource?.Dispose();
                }
                catch { }
                finally
                {
                    _cancellationTokenSource = null;
                }
            }
            catch { }
            finally
            {
                //release the semaphore for the listener to restart if wanted
                _semaphoreSlimListener.Release();
            }

            //Close the clientWebSocket
            CancellationTokenSource tokenSource = null;
            try
            {
                if (_clientWebSocket != null && (_clientWebSocket.State == WebSocketState.Open || _clientWebSocket.State == WebSocketState.Connecting))
                {
                    tokenSource = new CancellationTokenSource(ConnectionTimeout);

                   await _clientWebSocket.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, "",
                        tokenSource.Token);
                    while (_clientWebSocket.State != WebSocketState.Closed && !tokenSource.IsCancellationRequested) ;
                }
            }
            catch { }
            finally
            {
                tokenSource?.Dispose();
            }

            try
            {
                _clientWebSocket?.Dispose();
            }
            catch { }
            finally
            {
                _clientWebSocket = null;
            }

            //Let all the _requests the client is disconnecting and clear them
            try
            {
                foreach (var rpcStreamingResponseHandler in _requests)
                {
                    rpcStreamingResponseHandler.Value.HandleClientDisconnection();
                }
            }
            catch { }
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
