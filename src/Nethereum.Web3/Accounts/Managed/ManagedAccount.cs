using Nethereum.RPC.Accounts;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Web3.Accounts.Managed
{
    public class ManagedAccount : IAccount
    {
        public ManagedAccount(string accountAddress, string password)
        {
            Address = accountAddress;
            Password = password;
            InitialiseDefaultTransactionManager(password);
        }

        public string Address { get; protected set; }
        public string Password { get; protected set; }


        public ITransactionManager TransactionManager { get; protected set; }
        public INonceService NonceService { get; set; }

        protected virtual void InitialiseDefaultTransactionManager(string password)
        {
            TransactionManager = new ManagedAccountTransactionManager(Address, password);
        }
    }
}