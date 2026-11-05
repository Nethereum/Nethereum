using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class EthSendUserOperationHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;

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
                var entryPoint = UserOperationRpcParser.ParseEntryPoint(GetJsonElement(request, 1));

                var rpcUserOp = UserOperationRpcParser.ParseUserOperation(userOpJson);

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
                return BundlerRpcResponses.InternalError(request.Id, context, ex);
            }
        }
    }
}
