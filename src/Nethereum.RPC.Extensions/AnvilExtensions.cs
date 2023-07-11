namespace Nethereum.RPC.Extensions
{
    public static class AnvilExtensions
    {
        public static AnvilService Hardhat(this IEthApiService ethApiService)
        {
            return new AnvilService(ethApiService);
        }
    }


}