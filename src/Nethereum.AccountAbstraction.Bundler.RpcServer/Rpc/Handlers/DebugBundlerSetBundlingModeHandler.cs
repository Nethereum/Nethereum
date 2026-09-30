using Nethereum.CoreChain.Rpc;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.AccountAbstraction.Bundler.RpcServer.Rpc.Handlers
{
    public class DebugBundlerSetBundlingModeHandler : RpcHandlerBase
    {
        private readonly IBundlerServiceExtended _bundler;

        public DebugBundlerSetBundlingModeHandler(IBundlerServiceExtended bundler)
        {
            _bundler = bundler;
        }

        public override string MethodName => "debug_bundler_setBundlingMode";

        public override Task<RpcResponseMessage> HandleAsync(RpcRequestMessage request, RpcContext context)
        {
            try
            {
                var mode = GetParam<string>(request, 0);

                switch (mode?.ToLowerInvariant())
                {
                    case "auto":
                        _bundler.SetBundlingMode(BundlingMode.Auto);
                        break;
                    case "manual":
                        _bundler.SetBundlingMode(BundlingMode.Manual);
                        break;
                    default:
                        return Task.FromResult(Error(
                            request.Id, BundlerErrorCodes.InvalidFields, $"Invalid bundling mode '{mode}' (expected 'auto' or 'manual')"));
                }

                return Task.FromResult(Success(request.Id, "ok"));
            }
            catch (RpcException)
            {
                throw;
            }
            catch (Exception ex)
            {
                return Task.FromResult(Error(request.Id, BundlerErrorCodes.InternalError, $"Internal error: {ex.Message}"));
            }
        }
    }
}
