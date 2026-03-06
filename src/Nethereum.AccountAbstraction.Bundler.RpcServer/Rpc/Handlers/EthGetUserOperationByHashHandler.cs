using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
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
                var userOpHash = GetParam<string>(request, 0);

                if (string.IsNullOrEmpty(userOpHash))
                    throw RpcException.InvalidParams("userOpHash is required");

                var info = await _bundler.GetUserOperationByHashAsync(userOpHash);

                if (info == null)
                    return Success(request.Id, null);

                return Success(request.Id, new
                {
                    userOperation = new
                    {
                        sender = info.UserOperation.Sender,
                        nonce = ToHex(info.UserOperation.Nonce),
                        initCode = info.UserOperation.InitCode?.ToHex(true) ?? "0x",
                        callData = info.UserOperation.CallData?.ToHex(true) ?? "0x",
                        accountGasLimits = info.UserOperation.AccountGasLimits?.ToHex(true) ?? "0x",
                        preVerificationGas = ToHex(info.UserOperation.PreVerificationGas),
                        gasFees = info.UserOperation.GasFees?.ToHex(true) ?? "0x",
                        paymasterAndData = info.UserOperation.PaymasterAndData?.ToHex(true) ?? "0x",
                        signature = info.UserOperation.Signature?.ToHex(true) ?? "0x"
                    },
                    entryPoint = info.EntryPoint,
                    blockNumber = ToHex(info.BlockNumber),
                    transactionHash = info.TransactionHash
                });
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
