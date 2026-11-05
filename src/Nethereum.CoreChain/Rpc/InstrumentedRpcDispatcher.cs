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
    public class InstrumentedRpcDispatcher : IRpcDispatcher
    {
        private readonly RpcHandlerRegistry _registry;
        private readonly RpcContext _context;
        private readonly ILogger? _logger;
        private readonly JsonSerializerOptions _serializerOptions;
        private readonly RpcMetrics _metrics;

        public const string UnknownMethodMetric = "unknown";

        public int MaxBatchSize { get; set; } = RpcDispatcher.DefaultMaxBatchSize;

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

            var handler = _registry.GetHandler(methodName);
            var metricKey = handler?.MethodName ?? UnknownMethodMetric;

            using var timer = _metrics.MeasureRequest(metricKey);

            if (handler == null)
            {
                _logger?.LogDebug("RPC method not found: {Method}", RpcLogText.Method(methodName));
                _metrics.RecordError(metricKey, -32601);
                return CreateErrorResponse(request.Id, -32601, $"Method not found: {RpcLogText.Method(methodName)}");
            }

            try
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("RPC Request: {Method} Params: {Params}", RpcLogText.Method(methodName), RpcDispatcher.DescribeParams(request));
                }

                var response = await handler.HandleAsync(request, _context);

                if (response.HasError)
                {
                    _metrics.RecordError(metricKey, response.Error.Code);
                }

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    LogResult(RpcLogText.Method(methodName), response);
                }

                return response;
            }
            catch (RpcException ex)
            {
                RpcDispatcher.LogRpcException(_logger, ex, methodName);
                _metrics.RecordError(metricKey, ex.Code);
                return CreateErrorResponse(request.Id, ex.Code, ex.Message, ex.Data);
            }
            catch (Exception ex) when (RpcDispatcher.IsClientInputError(ex))
            {
                RpcDispatcher.LogClientInputError(_logger, methodName, ex);
                _metrics.RecordError(metricKey, -32602);
                return CreateErrorResponse(request.Id, -32602, "Invalid params");
            }
            catch (Exception ex) when (RpcDispatcher.IsExpectedStateError(ex))
            {
                RpcDispatcher.LogExpectedStateError(_logger, methodName, ex);
                _metrics.RecordError(metricKey, -32000);
                return CreateErrorResponse(request.Id, -32000, ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Internal error in {Method}: {Message}", RpcLogText.Method(methodName), ex.Message);
                _metrics.RecordError(metricKey, -32603);
                return CreateErrorResponse(request.Id, -32603, "Internal error");
            }
        }

        private void LogResult(string method, RpcResponseMessage response)
        {
            try
            {
                var resultJson = response.Result != null
                    ? JsonSerializer.Serialize(response.Result, _serializerOptions)
                    : "null";
                _logger.LogDebug("RPC Response: {Method} Result: {Result}", method, RpcLogText.Body(resultJson));
            }
            catch (Exception)
            {
            }
        }

        public async Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests)
        {
            var rejection = RpcDispatcher.RejectBatch(requests, MaxBatchSize);
            if (rejection != null)
            {
                return rejection;
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
