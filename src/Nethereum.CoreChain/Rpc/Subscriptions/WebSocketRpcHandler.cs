using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Model;

namespace Nethereum.CoreChain.Rpc.Subscriptions
{
    public class WebSocketRpcHandler
    {
        public const int MaxMessageSize = 32 * 1024 * 1024;
        public static readonly TimeSpan WriteTimeout = TimeSpan.FromSeconds(10);

        private const int ReceiveChunkSize = 16 * 1024;

        private readonly SubscriptionManager _subscriptionManager;
        private readonly IRpcDispatcher _dispatcher;
        private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
        private readonly ConcurrentDictionary<string, WebSocketOutbox> _outboxes = new();
        private readonly JsonSerializerOptions _serializerOptions;

        public bool SubscriptionsEnabled { get; set; } = true;

        public int LogQueryLimit { get; set; } = 1_000;

        public ConcurrentDictionary<string, WebSocket> Connections => _connections;
        public SubscriptionManager SubscriptionManager => _subscriptionManager;

        public WebSocketRpcHandler(
            SubscriptionManager subscriptionManager,
            RpcHandlerRegistry rpcRegistry,
            RpcContext rpcContext,
            JsonSerializerOptions serializerOptions = null)
            : this(subscriptionManager, new RpcDispatcher(rpcRegistry, rpcContext), serializerOptions)
        {
        }

        public WebSocketRpcHandler(
            SubscriptionManager subscriptionManager,
            IRpcDispatcher dispatcher,
            JsonSerializerOptions serializerOptions = null)
        {
            _subscriptionManager = subscriptionManager;
            _dispatcher = dispatcher;
            _serializerOptions = serializerOptions ?? JsonRpcEnvelope.CreateSerializerOptions();
        }

        public async Task HandleConnectionAsync(WebSocket ws, CancellationToken ct = default)
        {
            var connectionId = Guid.NewGuid().ToString("N");
            _connections[connectionId] = ws;
            var envelope = new JsonRpcEnvelope(new SubscriptionDispatcher(this, connectionId), _serializerOptions);

            try
            {
                var chunk = new byte[ReceiveChunkSize];
                using var message = new MemoryStream();
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    message.SetLength(0);
                    WebSocketReceiveResult received;
                    do
                    {
                        received = await ws.ReceiveAsync(new ArraySegment<byte>(chunk), ct);
                        if (received.MessageType == WebSocketMessageType.Close) return;

                        if (message.Length + received.Count > MaxMessageSize)
                        {
                            await ws.CloseOutputAsync(WebSocketCloseStatus.MessageTooBig, "message too big", default);
                            return;
                        }

                        message.Write(chunk, 0, received.Count);
                    }
                    while (!received.EndOfMessage);

                    if (received.MessageType != WebSocketMessageType.Text) continue;

                    var result = await envelope.HandleAsync(message.GetBuffer().AsMemory(0, (int)message.Length), ct);
                    if (result.Body != null)
                    {
                        GetOutbox(connectionId, ws).TryEnqueue(Encoding.UTF8.GetBytes(result.Body));
                    }
                }
            }
            catch (WebSocketException)
            {
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                Remove(connectionId);
                if (ws.State == WebSocketState.CloseReceived)
                {
                    try { await ws.CloseOutputAsync(WebSocketCloseStatus.NormalClosure, null, default); } catch { }
                }
                else if (ws.State == WebSocketState.Open)
                {
                    try { await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, null, default); } catch { }
                }
                ws.Dispose();
            }
        }

        public Task BroadcastBlockAsync(BlockHeader header, byte[] blockHash, List<FilteredLog> logs)
        {
            var notifications = _subscriptionManager.OnNewBlock(header, blockHash, logs);
            var payloads = new Dictionary<object, byte[]>(ReferenceEqualityComparer.Instance);
            var message = new ArrayBufferWriter<byte>();
            using var writer = new Utf8JsonWriter(message);

            foreach (var notification in notifications)
            {
                if (!_connections.TryGetValue(notification.ConnectionId, out var ws) || ws.State != WebSocketState.Open)
                {
                    continue;
                }

                if (!payloads.TryGetValue(notification.Payload, out var payload))
                {
                    payload = JsonSerializer.SerializeToUtf8Bytes<object>(notification.Payload, _serializerOptions);
                    payloads[notification.Payload] = payload;
                }

                message.Clear();
                writer.Reset(message);
                writer.WriteStartObject();
                writer.WriteString("jsonrpc", "2.0");
                writer.WriteString("method", "eth_subscription");
                writer.WriteStartObject("params");
                writer.WriteString("subscription", notification.SubscriptionId);
                writer.WritePropertyName("result");
                writer.WriteRawValue(payload, skipInputValidation: true);
                writer.WriteEndObject();
                writer.WriteEndObject();
                writer.Flush();

                GetOutbox(notification.ConnectionId, ws).TryEnqueue(message.WrittenSpan.ToArray());
            }

            return Task.CompletedTask;
        }

        private WebSocketOutbox GetOutbox(string connectionId, WebSocket ws) =>
            _outboxes.GetOrAdd(connectionId, id => new WebSocketOutbox(ws, WriteTimeout, () => Remove(id)));

        private void Remove(string connectionId)
        {
            _connections.TryRemove(connectionId, out _);
            _outboxes.TryRemove(connectionId, out _);
            _subscriptionManager.RemoveAllForConnection(connectionId);
        }

        private RpcResponseMessage Subscribe(RpcRequestMessage request, string connectionId)
        {
            string subType;
            LogFilter logFilter = null;
            try
            {
                var parameters = RequireParams(request);
                subType = parameters[0].GetString();
                if (subType == "logs" && parameters.GetArrayLength() > 1)
                    logFilter = ParseLogFilter(parameters[1], LogQueryLimit);
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is FormatException || ex is JsonException)
            {
                throw RpcException.InvalidParams("invalid subscription params");
            }

            if (subType != "newHeads" && subType != "logs")
                return new RpcResponseMessage(request.Id, new RpcError
                {
                    Code = -32000,
                    Message = $"no \"{RpcLogText.Method(subType)}\" subscription in eth namespace"
                });

            var type = subType == "logs" ? SubscriptionType.Logs : SubscriptionType.NewHeads;
            var subId = _subscriptionManager.Subscribe(connectionId, type, logFilter);
            return new RpcResponseMessage(request.Id, subId);
        }

        private RpcResponseMessage Unsubscribe(RpcRequestMessage request, string connectionId)
        {
            string subId;
            try
            {
                subId = RequireParams(request)[0].GetString() ?? "";
            }
            catch (Exception ex) when (ex is InvalidOperationException || ex is FormatException)
            {
                throw RpcException.InvalidParams("invalid subscription id");
            }

            return new RpcResponseMessage(request.Id, _subscriptionManager.Unsubscribe(subId, connectionId));
        }

        private static JsonElement RequireParams(RpcRequestMessage request)
        {
            if (request.RawParameters is JsonElement parameters
                && parameters.ValueKind == JsonValueKind.Array
                && parameters.GetArrayLength() > 0)
            {
                return parameters;
            }

            throw RpcException.InvalidParams("missing value for required argument 0");
        }

        private static LogFilter ParseLogFilter(JsonElement filterObj, int logQueryLimit)
        {
            var filter = new LogFilter();

            if (filterObj.TryGetProperty("address", out var addrProp))
            {
                if (addrProp.ValueKind == JsonValueKind.String)
                    filter.Addresses.Add(addrProp.GetString());
                else if (addrProp.ValueKind == JsonValueKind.Array)
                    foreach (var a in addrProp.EnumerateArray())
                        filter.Addresses.Add(a.GetString());
            }

            if (filterObj.TryGetProperty("topics", out var topicsProp) && topicsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var topicPosition in topicsProp.EnumerateArray())
                {
                    if (topicPosition.ValueKind == JsonValueKind.Null)
                        filter.Topics.Add(null);
                    else if (topicPosition.ValueKind == JsonValueKind.String)
                        filter.Topics.Add(new List<byte[]> { topicPosition.GetString().HexToByteArray() });
                    else if (topicPosition.ValueKind == JsonValueKind.Array)
                    {
                        var topicList = new List<byte[]>();
                        foreach (var t in topicPosition.EnumerateArray())
                            topicList.Add(t.GetString().HexToByteArray());
                        filter.Topics.Add(topicList);
                    }
                }
            }

            LogFilterLimits.Enforce(filter, logQueryLimit);
            return filter;
        }

        private sealed class SubscriptionDispatcher : IRpcDispatcher
        {
            private readonly WebSocketRpcHandler _owner;
            private readonly string _connectionId;

            public SubscriptionDispatcher(WebSocketRpcHandler owner, string connectionId)
            {
                _owner = owner;
                _connectionId = connectionId;
            }

            public int MaxBatchSize
            {
                get => _owner._dispatcher.MaxBatchSize;
                set => _owner._dispatcher.MaxBatchSize = value;
            }

            public async Task<RpcResponseMessage> DispatchAsync(RpcRequestMessage request)
            {
                if (!_owner.SubscriptionsEnabled || (request?.Method != "eth_subscribe" && request?.Method != "eth_unsubscribe"))
                {
                    return await _owner._dispatcher.DispatchAsync(request);
                }

                try
                {
                    return request.Method == "eth_subscribe"
                        ? _owner.Subscribe(request, _connectionId)
                        : _owner.Unsubscribe(request, _connectionId);
                }
                catch (RpcException ex)
                {
                    return new RpcResponseMessage(request.Id, new RpcError { Code = ex.Code, Message = ex.Message });
                }
            }

            public async Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests)
            {
                var responses = new List<RpcResponseMessage>();
                foreach (var request in requests)
                {
                    responses.Add(await DispatchAsync(request));
                }

                return responses;
            }
        }
    }
}
