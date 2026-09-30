using Nethereum.CoreChain.Rpc;
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

                var result = filtered
                    .Select(p => AccountAbstraction.UserOperationConverter.ToRpcFormat(p.UserOperation).ToUnpackedRpcDictionary())
                    .ToArray();

                return Success(request.Id, result);
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}
