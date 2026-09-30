using Nethereum.EVM;

namespace Nethereum.CoreChain.Rpc.Handlers.Engine
{
    public static class EngineForkGuard
    {
        public static void RequireAtLeast(RpcContext context, HardforkName minimumFork, long blockNumber, ulong timestamp)
        {
            var fork = context.Node.Config.ResolveHardforkAt(blockNumber, timestamp);
            if (fork < minimumFork)
                throw new RpcException(-38005, "Unsupported fork");
        }
    }
}
