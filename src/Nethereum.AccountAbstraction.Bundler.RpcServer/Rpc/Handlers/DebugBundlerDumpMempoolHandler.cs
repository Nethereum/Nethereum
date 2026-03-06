using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerDumpMempoolHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerDumpMempoolHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_dumpMempool";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var entryPoint = GetParam<string>(request, 0);
                var pending = await _bundler.GetPendingUserOperationsAsync();

                var filtered = string.IsNullOrEmpty(entryPoint)
                    ? pending
                    : pending.Where(p => p.EntryPoint.Equals(entryPoint, StringComparison.OrdinalIgnoreCase)).ToArray();

                var result = filtered.Select(p => new
                {
                    userOperation = new
                    {
                        sender = p.UserOperation.Sender,
                        nonce = ToHex(p.UserOperation.Nonce),
                        initCode = p.UserOperation.InitCode?.ToHex(true) ?? "0x",
                        callData = p.UserOperation.CallData?.ToHex(true) ?? "0x",
                        accountGasLimits = p.UserOperation.AccountGasLimits?.ToHex(true) ?? "0x",
                        preVerificationGas = ToHex(p.UserOperation.PreVerificationGas),
                        gasFees = p.UserOperation.GasFees?.ToHex(true) ?? "0x",
                        paymasterAndData = p.UserOperation.PaymasterAndData?.ToHex(true) ?? "0x",
                        signature = p.UserOperation.Signature?.ToHex(true) ?? "0x"
                    },
                    entryPoint = p.EntryPoint,
                    userOpHash = p.UserOpHash,
                    submittedAt = p.SubmittedAt.ToUnixTimeSeconds(),
                    retryCount = p.RetryCount
                }).ToArray();

                return Success(request.Id, result);
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
