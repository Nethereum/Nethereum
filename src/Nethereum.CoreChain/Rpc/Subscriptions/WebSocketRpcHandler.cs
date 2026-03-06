using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        private readonly SubscriptionManager _subscriptionManager;
        private readonly RpcHandlerRegistry _rpcRegistry;
        private readonly RpcContext _rpcContext;
        private readonly ConcurrentDictionary<string, WebSocket> _connections = new();
        private readonly JsonSerializerOptions _serializerOptions;

        public ConcurrentDictionary<string, WebSocket> Connections => _connections;
        public SubscriptionManager SubscriptionManager => _subscriptionManager;

        public WebSocketRpcHandler(
            SubscriptionManager subscriptionManager,
            RpcHandlerRegistry rpcRegistry,
            RpcContext rpcContext,
            JsonSerializerOptions serializerOptions = null)
        {
            _subscriptionManager = subscriptionManager;
            _rpcRegistry = rpcRegistry;
            _rpcContext = rpcContext;
            _serializerOptions = serializerOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task HandleConnectionAsync(WebSocket ws, CancellationToken ct = default)
        {
            var connectionId = Guid.NewGuid().ToString("N");
            _connections[connectionId] = ws;

            try
            {
                var buffer = new byte[4096];
                while (ws.State == WebSocketState.Open && !ct.IsCancellationRequested)
                {
                    var received = await ws.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
                    if (received.MessageType == WebSocketMessageType.Close)
                        break;

                    if (received.MessageType != WebSocketMessageType.Text)
                        continue;

                    var requestJson = Encoding.UTF8.GetString(buffer, 0, received.Count);
                    var responseJson = await ProcessRequestAsync(requestJson, connectionId);

                    var responseBytes = Encoding.UTF8.GetBytes(responseJson);
                    await ws.SendAsync(new ArraySegment<byte>(responseBytes), WebSocketMessageType.Text, true, ct);
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
                _connections.TryRemove(connectionId, out _);
                _subscriptionManager.RemoveAllForConnection(connectionId);
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

        public async Task BroadcastBlockAsync(BlockHeader header, byte[] blockHash, List<FilteredLog> logs)
        {
            var notifications = _subscriptionManager.OnNewBlock(header, blockHash, logs);
            if (notifications.Count == 0) return;

            foreach (var notification in notifications)
            {
                if (_connections.TryGetValue(notification.ConnectionId, out var ws) && ws.State == WebSocketState.Open)
                {
                    try
                    {
                        var msg = JsonSerializer.Serialize(new
                        {
                            jsonrpc = "2.0",
                            method = "eth_subscription",
                            @params = new
                            {
                                subscription = notification.SubscriptionId,
                                result = notification.Payload
                            }
                        }, _serializerOptions);
                        var bytes = Encoding.UTF8.GetBytes(msg);
                        await ws.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, default);
                    }
                    catch (WebSocketException)
                    {
                        _connections.TryRemove(notification.ConnectionId, out _);
                        _subscriptionManager.RemoveAllForConnection(notification.ConnectionId);
                    }
                }
            }
        }

        private async Task<string> ProcessRequestAsync(string requestJson, string connectionId)
        {
            try
            {
                var jsonRequest = JsonSerializer.Deserialize<JsonRpcRequest>(requestJson, _serializerOptions);
                if (jsonRequest == null || string.IsNullOrEmpty(jsonRequest.Method))
                    return "{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{\"code\":-32600,\"message\":\"Invalid Request\"}}";

                if (jsonRequest.Method == "eth_subscribe")
                    return HandleSubscribe(jsonRequest, connectionId);

                if (jsonRequest.Method == "eth_unsubscribe")
                    return HandleUnsubscribe(jsonRequest, connectionId);

                var handler = _rpcRegistry.GetHandler(jsonRequest.Method);
                if (handler == null)
                    return JsonSerializer.Serialize(new JsonRpcResponse
                    {
                        Id = jsonRequest.Id,
                        Error = new JsonRpcError { Code = -32601, Message = $"Method not found: {jsonRequest.Method}" }
                    }, _serializerOptions);

                var request = new RpcRequestMessage(jsonRequest.Id, jsonRequest.Method);
                if (jsonRequest.Params.HasValue)
                    request.RawParameters = jsonRequest.Params.Value;

                var response = await handler.HandleAsync(request, _rpcContext);
                var jsonResponse = new JsonRpcResponse
                {
                    Id = response.Id,
                    Result = response.HasError ? null : response.Result,
                    Error = response.HasError ? new JsonRpcError { Code = response.Error.Code, Message = response.Error.Message, Data = response.Error.Data } : null
                };
                return JsonSerializer.Serialize(jsonResponse, _serializerOptions);
            }
            catch (Exception ex)
            {
                return $"{{\"jsonrpc\":\"2.0\",\"id\":null,\"error\":{{\"code\":-32603,\"message\":\"{ex.Message.Replace("\"", "'")}\"}}}}";
            }
        }

        private string HandleSubscribe(JsonRpcRequest request, string connectionId)
        {
            var subType = "newHeads";
            LogFilter logFilter = null;

            if (request.Params.HasValue)
            {
                var paramsElement = request.Params.Value;
                if (paramsElement.ValueKind == JsonValueKind.Array && paramsElement.GetArrayLength() > 0)
                {
                    subType = paramsElement[0].GetString() ?? "newHeads";

                    if (subType == "logs" && paramsElement.GetArrayLength() > 1)
                        logFilter = ParseLogFilter(paramsElement[1]);
                }
            }

            var type = subType == "logs" ? SubscriptionType.Logs : SubscriptionType.NewHeads;
            var subId = _subscriptionManager.Subscribe(connectionId, type, logFilter);
            return JsonSerializer.Serialize(new JsonRpcResponse { Id = request.Id, Result = subId }, _serializerOptions);
        }

        private string HandleUnsubscribe(JsonRpcRequest request, string connectionId)
        {
            var subId = "";
            if (request.Params.HasValue)
            {
                var paramsElement = request.Params.Value;
                if (paramsElement.ValueKind == JsonValueKind.Array && paramsElement.GetArrayLength() > 0)
                    subId = paramsElement[0].GetString() ?? "";
            }

            var result = _subscriptionManager.Unsubscribe(subId, connectionId);
            return JsonSerializer.Serialize(new JsonRpcResponse { Id = request.Id, Result = result }, _serializerOptions);
        }

        private static LogFilter ParseLogFilter(JsonElement filterObj)
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

            return filter;
        }
    }
}
