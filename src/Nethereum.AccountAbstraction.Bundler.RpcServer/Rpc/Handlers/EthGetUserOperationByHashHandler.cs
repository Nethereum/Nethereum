using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class EthGetUserOperationByHashHandler : RpcHandlerBase
    {
        private readonly IBundlerService _bundler;

        public EthGetUserOperationByHashHandler(IBundlerService bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "eth_getUserOperationByHash";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var userOpHash = UserOperationRpcParser.ParseUserOpHash(GetJsonElement(request, 0));

                var info = await _bundler.GetUserOperationByHashAsync(userOpHash);

                if (info == null)
                    return Success(request.Id, null);

                // ERC-7769: the userOperation is returned in the unpacked v0.7+ wire format,
                // and blockNumber/blockHash/transactionHash are null while pending.
                var rpcUserOp = AccountAbstraction.UserOperationConverter.ToRpcFormat(info.UserOperation);
                var included = info.TransactionHash != null;

                var userOperation = rpcUserOp.ToUnpackedRpcDictionary();

                return Success(request.Id, new
                {
                    userOperation,
                    entryPoint = info.EntryPoint,
                    blockNumber = included ? ToHex(info.BlockNumber) : null,
                    blockHash = included ? info.BlockHash : null,
                    transactionHash = info.TransactionHash
                });
            }
            catch (RpcException)
            {
                throw;
            }
            catch (BundlerRpcException ex)
            {
                return Error(request.Id, ex.Code, ex.Message, ex.ErrorData);
            }
            catch (Exception ex)
            {
                return BundlerRpcResponses.InternalError(request.Id, context, ex);
            }
        }
    }
}
