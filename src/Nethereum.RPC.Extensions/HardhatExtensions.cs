using Nethereum.RPC;

namespace Nethereum.RPC.Extensions
{
    public static class HardhatExtensions
    {
        public static HardhatService Hardhat(this IEthApiService ethApiService)
        {
            return new HardhatService(ethApiService);
        }
    }


}