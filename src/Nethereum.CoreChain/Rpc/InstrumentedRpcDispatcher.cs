using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Metrics;
using Nethereum.JsonRpc.Client.RpcMessages;
using RpcMessages = Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public class InstrumentedRpcDispatcher
    {
        private readonly RpcHandlerRegistry _registry;
        private readonly RpcContext _context;
        private readonly ILogger? _logger;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly RpcMetrics _metrics;

        public InstrumentedRpcDispatcher(
            RpcHandlerRegistry registry,
            RpcContext context,
            RpcMetrics metrics,
            ILogger? logger = null,
            JsonSerializerOptions? serializerOptions = null)
        {
            ArgumentNullException.ThrowIfNull(registry);
            ArgumentNullException.ThrowIfNull(context);
            ArgumentNullException.ThrowIfNull(metrics);

            _registry = registry;
            _context = context;
            _metrics = metrics;
            _logger = logger;
            _serializerOptions = serializerOptions ?? new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = false
            };
        }

        public async Task<RpcResponseMessage> DispatchAsync(RpcRequestMessage request)
        {
            if (request == null)
            {
                _metrics.RecordError("null_request", -32600);
                return CreateErrorResponse(null, -32600, "Invalid Request");
            }

            if (string.IsNullOrEmpty(request.Method))
            {
                _metrics.RecordError("invalid_request", -32600);
                return CreateErrorResponse(request.Id, -32600, "Invalid Request: method is required");
            }

            var methodName = request.Method;

            using var timer = _metrics.MeasureRequest(methodName);

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                var paramsJson = request.RawParameters != null
                    ? JsonSerializer.Serialize(request.RawParameters, _serializerOptions)
                    : "null";
                _logger.LogDebug("RPC Request: {Method} Params: {Params}", methodName, paramsJson);
            }

            var handler = _registry.GetHandler(methodName);
            if (handler == null)
            {
                _logger?.LogWarning("RPC method not found: {Method}", methodName);
                _metrics.RecordError(methodName, -32601);
                return CreateErrorResponse(request.Id, -32601, $"Method not found: {methodName}");
            }

            try
            {
                var response = await handler.HandleAsync(request, _context);

                if (response.HasError)
                {
                    _metrics.RecordError(methodName, response.Error.Code);
                }

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    var resultJson = response.Result != null
                        ? JsonSerializer.Serialize(response.Result, _serializerOptions)
                        : "null";
                    _logger.LogDebug("RPC Response: {Method} Result: {Result}", methodName, resultJson);
                }

                return response;
            }
            catch (RpcException ex)
            {
                _logger?.LogError(ex, "RPC error in {Method}: Code={Code} Message={Message}",
                    methodName, ex.Code, ex.Message);
                _metrics.RecordError(methodName, ex.Code);
                return CreateErrorResponse(request.Id, ex.Code, ex.Message, ex.Data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Internal error in {Method}: {Message}", methodName, ex.Message);
                _metrics.RecordError(methodName, -32603);
                return CreateErrorResponse(request.Id, -32603, "Internal error");
            }
        }

        public async Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests)
        {
            if (requests == null || requests.Length == 0)
            {
                return new List<RpcResponseMessage> { CreateErrorResponse(null, -32600, "Invalid Request") };
            }

            _logger?.LogDebug("RPC Batch Request: {Count} requests", requests.Length);

            var responses = new List<RpcResponseMessage>();
            foreach (var request in requests)
            {
                var response = await DispatchAsync(request);
                responses.Add(response);
            }

            return responses;
        }

        private RpcResponseMessage CreateErrorResponse(object? id, int code, string message, object? data = null)
        {
            var error = new RpcMessages.RpcError
            {
                Code = code,
                Message = message,
                Data = data
            };
            return new RpcResponseMessage(id, error);
        }
    }
}
