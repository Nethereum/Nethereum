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
                // The EIP-7702 auth is a side-channel not folded into the packed form, so carry it
                // alongside the packed op into the bundler (v0.9 EntryPoint). Null when unset.
                var userOpHash = await _bundler.SendUserOperationAsync(packedUserOp, entryPoint, rpcUserOp.Eip7702Auth);

                return Success(request.Id, userOpHash);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (BundlerRpcException ex)
            {
                return Error(request.Id, ex.Code, ex.Message, ex.ErrorData);
            }
            catch (JsonException ex)
            {
                return Error(request.Id, BundlerErrorCodes.InvalidFields, $"Invalid UserOperation format: {ex.Message}");
            }
            catch (ArgumentException ex)
            {
                return Error(request.Id, BundlerErrorCodes.InvalidFields, ex.Message);
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
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
