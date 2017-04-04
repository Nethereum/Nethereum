using Nethereum.RPC.Eth.TransactionManagers;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Web3.Accounts
{
    public class ManagedAccount : IAccount
    {
        public ManagedAccount(string accountAddress, string password)
        {
            Address = accountAddress;
            InitialiseDefaultTransactionManager(password);
        }

        public string Address { get; protected set; }
        public ITransactionManager TransactionManager { get; protected set; }

        protected virtual void InitialiseDefaultTransactionManager(string password)
        {
            TransactionManager = new ManagedAccountTransactionManager(Address, password);
        }
    }
}