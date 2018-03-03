namespace Nethereum.Accounts.IntegrationTests
{
    public static class Web3Factory
    {
        public static Web3.Web3 GetWeb3()
        {
            return new Web3.Web3(AccountFactory.GetAccount(), ClientFactory.GetClient());
        }

        public static Web3.Web3 GetWeb3Managed()
        {
            return new Web3.Web3(AccountFactory.GetManagedAccount(), ClientFactory.GetClient());
        }
    }
}