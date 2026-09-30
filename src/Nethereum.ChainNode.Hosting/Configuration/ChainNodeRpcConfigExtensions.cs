using Nethereum.CoreChain;

namespace Nethereum.ChainNode.Hosting.Configuration
{
    public static class ChainNodeRpcConfigExtensions
    {
        public static void ApplyTo(this ChainNodeRpcConfig rpc, ChainConfig target)
        {
            target.RpcMaxLogBlockRange = rpc.MaxLogBlockRange;
            target.RpcMaxLogResults = rpc.MaxLogResults;
            target.RpcGasCap = rpc.GasCap;
        }
    }
}
