using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerDumpReputationHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerDumpReputationHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_dumpReputation";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                GetOptionalParam<string>(request, 0, string.Empty);

                var entries = await _bundler.GetAllReputationAsync();

                // Reference bundler shape (ReputationManager.dump(), deepHexlify'd for the
                // wire): {address, opsSeen, opsIncluded, status}. opsFailed/opsDropped are
                // Nethereum-internal counters with no reference equivalent and must not
                // leak here - the ERC-7562 compliance suite constructs a strict dataclass
                // from this response and rejects unexpected fields. status is the numeric
                // ReputationStatus (OK=0, THROTTLED=1, BANNED=2), not a status word.
                var result = entries.Select(reputation => new
                {
                    address = reputation.Address,
                    opsSeen = reputation.OpsSeen,
                    opsIncluded = reputation.OpsIncluded,
                    status = (int)reputation.Status
                }).ToArray();

                return Success(request.Id, result);
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}
