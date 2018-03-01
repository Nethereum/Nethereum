namespace Nethereum.Web3.IntegrationTests
{
    public static class Web3Factory
    {
        public static Web3 GetWeb3()
        {
            return new Web3(AccountFactory.GetAccount());
        }

        public static Web3 GetWeb3Managed()
        {
            return new Web3(AccountFactory.GetManagedAccount());
        }
    }
}