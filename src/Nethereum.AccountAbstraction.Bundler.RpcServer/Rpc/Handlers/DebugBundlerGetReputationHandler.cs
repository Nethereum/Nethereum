using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerGetReputationHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerGetReputationHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_dumpReputation";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var address = GetOptionalParam<string>(request, 0, string.Empty);

                if (string.IsNullOrEmpty(address))
                {
                    return Success(request.Id, Array.Empty<object>());
                }

                var reputation = await _bundler.GetReputationAsync(address);

                return Success(request.Id, new[]
                {
                    new
                    {
                        address = reputation.Address,
                        opsIncluded = reputation.OpsIncluded,
                        opsFailed = reputation.OpsFailed,
                        status = reputation.Status.ToString().ToLowerInvariant()
                    }
                });
            }
            catch (Exception ex)
            {
                return Error(request.Id, -32603, $"Internal error: {ex.Message}");
            }
        }
    }
}
