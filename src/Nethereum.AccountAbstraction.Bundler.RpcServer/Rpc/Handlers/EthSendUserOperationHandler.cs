using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class EthSendUserOperationHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;
        private static readonly JsonSerializerOptions JsonOptions = CreateJsonOptions();

        public EthSendUserOperationHandler(IBundlerService bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "eth_sendUserOperation";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var userOpJson = GetJsonElement(request, 0);
                var entryPoint = GetParam<string>(request, 1);

                if (string.IsNullOrEmpty(entryPoint))
                    throw RpcException.InvalidParams("entryPoint address is required");

                var rpcUserOp = JsonSerializer.Deserialize<RpcUserOperation>(userOpJson.GetRawText(), JsonOptions)
                    ?? throw new JsonException("Failed to deserialize UserOperation");

                var packedUserOp = rpcUserOp.ToPackedUserOperation();
                var userOpHash = await _bundler.SendUserOperationAsync(packedUserOp, entryPoint);

                return Success(request.Id, userOpHash);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (JsonException ex)
            {
                return Error(request.Id, -32602, $"Invalid UserOperation format: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return Error(request.Id, -32602, ex.Message);
            }
            catch (InvalidOperationException ex)
            {
                return Error(request.Id, -32500, ex.Message);
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }

        private static JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };
            return options;
        }
    }
}
