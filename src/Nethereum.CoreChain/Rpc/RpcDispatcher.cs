using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcDispatcher : IRpcDispatcher
    {
        private readonly RpcHandlerRegistry _registry;
        private readonly RpcContext _context;
        private readonly ILogger? _logger;
        private readonly JsonSerializerOptions _serializerOptions;

        public const int DefaultMaxBatchSize = 1000;

        public int MaxBatchSize { get; set; } = DefaultMaxBatchSize;

        public RpcDispatcher(RpcHandlerRegistry registry, RpcContext context)
            : this(registry, context, null, null)
        {
        }

        public RpcDispatcher(
            RpcHandlerRegistry registry,
            RpcContext context,
            ILogger? logger,
            JsonSerializerOptions? serializerOptions = null)
        {
            _registry = registry;
            _context = context;
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
                return CreateErrorResponse(null, -32600, "Invalid Request");
            }

            if (string.IsNullOrEmpty(request.Method))
            {
                return CreateErrorResponse(request.Id, -32600, "Invalid Request: method is required");
            }

            var handler = _registry.GetHandler(request.Method);
            if (handler == null)
            {
                _logger?.LogDebug("RPC method not found: {Method}", RpcLogText.Method(request.Method));
                return CreateErrorResponse(request.Id, -32601, $"Method not found: {RpcLogText.Method(request.Method)}");
            }

            try
            {
                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    _logger.LogDebug("RPC Request: {Method} Params: {Params}", RpcLogText.Method(request.Method), DescribeParams(request));
                }

                var response = await handler.HandleAsync(request, _context);

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    LogResult(RpcLogText.Method(request.Method), response);
                }

                return response;
            }
            catch (RpcException ex)
            {
                LogRpcException(_logger, ex, request.Method);
                return CreateErrorResponse(request.Id, ex.Code, ex.Message, ex.Data);
            }
            catch (Exception ex) when (RpcDispatcher.IsClientInputError(ex))
            {
                LogClientInputError(_logger, request.Method, ex);
                return CreateErrorResponse(request.Id, -32602, "Invalid params");
            }
            catch (Exception ex) when (RpcDispatcher.IsExpectedStateError(ex))
            {
                LogExpectedStateError(_logger, request.Method, ex);
                return CreateErrorResponse(request.Id, -32000, ex.Message);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Internal error in {Method}: {Message}", RpcLogText.Method(request.Method), ex.Message);
                return CreateErrorResponse(request.Id, -32603, "Internal error");
            }
        }

        public async Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests)
        {
            var rejection = RejectBatch(requests, MaxBatchSize);
            if (rejection != null)
            {
                return rejection;
            }

            _logger?.LogDebug("RPC Batch Request: {Count} requests", requests.Length);

            var responses = new List<RpcResponseMessage>();
            foreach (var request in requests)
            {
                responses.Add(await DispatchAsync(request));
            }

            _logger?.LogDebug("RPC Batch Response: {Count} responses", responses.Count);

            return responses;
        }

        internal static string DescribeParams(RpcRequestMessage request) =>
            RpcLogText.Body(request.RawParameters is JsonElement element ? element.GetRawText() : request.RawParameters?.ToString());

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

        internal static bool IsClientInputError(Exception exception) => exception is FormatException;

        internal static bool IsExpectedStateError(Exception exception) => exception is Nethereum.CoreChain.Storage.HistoricalStateNotAvailableException;

        internal static void LogClientInputError(ILogger? logger, string method, Exception exception) =>
            logger?.LogDebug("Invalid params in {Method}: {Reason}", RpcLogText.Method(method), RpcLogText.Method(exception.Message));

        internal static void LogExpectedStateError(ILogger? logger, string method, Exception exception) =>
            logger?.LogDebug("State unavailable in {Method}: {Reason}", RpcLogText.Method(method), RpcLogText.Method(exception.Message));

        internal static void LogRpcException(ILogger? logger, RpcException exception, string method)
        {
            if (logger == null) return;

            if (IsClientErrorCode(exception.Code))
            {
                logger.LogDebug("RPC error in {Method}: Code={Code} Message={Message}",
                    RpcLogText.Method(method), exception.Code, exception.Message);
                return;
            }

            logger.LogError(exception, "RPC error in {Method}: Code={Code} Message={Message}",
                RpcLogText.Method(method), exception.Code, exception.Message);
        }

        private static bool IsClientErrorCode(int code) =>
            code == -32600 || code == -32601 || code == -32602 || code == -32000 || code == 3 || (code <= -38000 && code > -39000);

        public static List<RpcResponseMessage>? RejectBatch(RpcRequestMessage[] requests, int maxBatchSize)
        {
            if (requests == null || requests.Length == 0)
            {
                return new List<RpcResponseMessage> { CreateErrorResponse(null, -32600, "Invalid Request") };
            }

            if (requests.Length > maxBatchSize)
            {
                return BatchTooLarge(requests[0]?.Id);
            }

            return null;
        }

        public static List<RpcResponseMessage> BatchTooLarge(object? firstId) =>
            new List<RpcResponseMessage> { CreateErrorResponse(firstId, -32600, "batch too large") };

        private static RpcResponseMessage CreateErrorResponse(object id, int code, string message, object data = null)
        {
            return new RpcResponseMessage(id, new RpcError
            {
                Code = code,
                Message = message,
                Data = data
            });
        }
    }
}
