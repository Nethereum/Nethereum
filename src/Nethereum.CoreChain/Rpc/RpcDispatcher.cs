using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcDispatcher
    {
        private readonly RpcHandlerRegistry _registry;
        private readonly RpcContext _context;
        private readonly ILogger? _logger;
        private readonly JsonSerializerOptions _serializerOptions;

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

            if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
            {
                var paramsJson = request.RawParameters != null
                    ? JsonSerializer.Serialize(request.RawParameters, _serializerOptions)
                    : "null";
                _logger.LogDebug("RPC Request: {Method} Params: {Params}", request.Method, paramsJson);
            }

            var handler = _registry.GetHandler(request.Method);
            if (handler == null)
            {
                _logger?.LogWarning("RPC method not found: {Method}", request.Method);
                return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
            }

            try
            {
                var response = await handler.HandleAsync(request, _context);

                if (_logger != null && _logger.IsEnabled(LogLevel.Debug))
                {
                    var resultJson = response.Result != null
                        ? JsonSerializer.Serialize(response.Result, _serializerOptions)
                        : "null";
                    _logger.LogDebug("RPC Response: {Method} Result: {Result}", request.Method, resultJson);
                }

                return response;
            }
            catch (RpcException ex)
            {
                _logger?.LogError(ex, "RPC error in {Method}: Code={Code} Message={Message}",
                    request.Method, ex.Code, ex.Message);
                return CreateErrorResponse(request.Id, ex.Code, ex.Message, ex.Data);
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "Internal error in {Method}: {Message}", request.Method, ex.Message);
                return CreateErrorResponse(request.Id, -32603, $"Internal error: {ex.Message}");
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
                responses.Add(await DispatchAsync(request));
            }

            _logger?.LogDebug("RPC Batch Response: {Count} responses", responses.Count);

            return responses;
        }

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
