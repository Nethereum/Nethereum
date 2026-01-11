using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.CoreChain.Rpc
{
    public class RpcDispatcher
    {
        private readonly RpcHandlerRegistry _registry;
        private readonly RpcContext _context;
        private readonly Action<string> _logInfo;
        private readonly Action<string, Exception> _logError;

        public RpcDispatcher(RpcHandlerRegistry registry, RpcContext context, Action<string> logInfo = null, Action<string, Exception> logError = null)
        {
            _registry = registry;
            _context = context;
            _logInfo = logInfo;
            _logError = logError;
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

            _logInfo?.Invoke($"RPC Request: {request.Method}");

            var handler = _registry.GetHandler(request.Method);
            if (handler == null)
            {
                _logInfo?.Invoke($"Method not found: {request.Method}");
                return CreateErrorResponse(request.Id, -32601, $"Method not found: {request.Method}");
            }

            try
            {
                return await handler.HandleAsync(request, _context);
            }
            catch (RpcException ex)
            {
                _logError?.Invoke($"RPC error in {request.Method}: {ex.Message}", ex);
                return CreateErrorResponse(request.Id, ex.Code, ex.Message, ex.Data);
            }
            catch (Exception ex)
            {
                _logError?.Invoke($"Internal error in {request.Method}: {ex.Message}", ex);
                return CreateErrorResponse(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }

        public async Task<List<RpcResponseMessage>> DispatchBatchAsync(RpcRequestMessage[] requests)
        {
            if (requests == null || requests.Length == 0)
            {
                return new List<RpcResponseMessage> { CreateErrorResponse(null, -32600, "Invalid Request") };
            }

            var responses = new List<RpcResponseMessage>();
            foreach (var request in requests)
            {
                responses.Add(await DispatchAsync(request));
            }

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
