using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerGetStakeStatusHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerGetStakeStatusHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_getStakeStatus";

        public override async Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var address = GetParam<string>(request, 0);
                var entryPoint = GetParam<string>(request, 1);

                var status = await _bundler.GetStakeStatusAsync(address, entryPoint);

                var result = new
                {
                    stakeInfo = new
                    {
                        addr = status.Address,
                        stake = status.Stake.ToString(),
                        unstakeDelaySec = status.UnstakeDelaySec.ToString()
                    },
                    isStaked = status.IsStaked
                };

                return Success(request.Id, result);
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}");
            }
        }
    }
}
