namespace Nethereum.Web3.Sample
{
    public static class GethTesterFactory
    {
        public static GethTester GetLocal(Web3 web3)
        {
            return new GethTester(web3, "0x12890d2cce102216644c59dae5baed380d84830c", "password");
        }
    }
}