using Nethereum.RPC.Accounts;
using Nethereum.RPC.AccountSigning;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Web3.Accounts.Basic
{
    public class BasicAccount : IAccount
    {
        public BasicAccount(string accountAddress)
        {
            Address = accountAddress;
            InitialiseDefaultTransactionManager();
        }

        public BasicAccount(string accountAddress,
            BasicAccountTransactionManager transactionManager)
        {
            Address = accountAddress;
            TransactionManager = transactionManager;
            transactionManager.SetAccount(this);
        }

        public string Address { get; protected set; }

        public ITransactionManager TransactionManager { get; protected set; }

        public INonceService NonceService { get; set; }

        public IAccountSigningService AccountSigningService { get; private set; }

        protected virtual void InitialiseDefaultTransactionManager()
        {
            TransactionManager = new BasicAccountTransactionManager(null, this);
        }
    }
}