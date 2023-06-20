using Nethereum.RPC;

namespace Nethereum.RPC.Extensions
{
    public static class EvmToolsExtensions
    {
        public static EvmToolsService DevToolsEvm(this IEthApiService ethApiService)
        {
            return new EvmToolsService(ethApiService);
        }
    }


}