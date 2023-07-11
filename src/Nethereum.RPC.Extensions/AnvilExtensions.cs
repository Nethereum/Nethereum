namespace Nethereum.RPC.Extensions
{
    public static class AnvilExtensions
    {
        public static AnvilService Anvil(this IEthApiService ethApiService)
        {
            return new AnvilService(ethApiService);
        }
    }


}