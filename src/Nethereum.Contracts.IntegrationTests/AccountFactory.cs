using Nethereum.Web3.Accounts;
using Nethereum.Web3.Accounts.Managed;

namespace Nethereum.Contracts.IntegrationTests
{
    public static class AccountFactory
    {
        public static string PrivateKey = "0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7";
        public static string Address = "0x12890d2cce102216644c59daE5baed380d84830c";
        public static string Password = "password";

        public static Account GetAccount()
        {
            return new Account(PrivateKey);
        }

        public static ManagedAccount GetManagedAccount()
        {
            return new ManagedAccount(Address, Password);
        }
    }
}