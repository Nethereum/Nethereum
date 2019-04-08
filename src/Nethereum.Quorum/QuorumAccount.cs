using Nethereum.RPC.Accounts;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Quorum
{
    public class QuorumAccount : IAccount
    {
        public QuorumAccount(string accountAddress)
        {
            Address = accountAddress;
            InitialiseDefaultTransactionManager();
        }

        public QuorumAccount(string accountAddress,
            QuorumTransactionManager transactionManager)
        {
            Address = accountAddress;
            TransactionManager = transactionManager;
            transactionManager.SetAccount(this);
        }

        public string Address { get; protected set; }

        public ITransactionManager TransactionManager { get; protected set; }

        public INonceService NonceService { get; set; }

        protected virtual void InitialiseDefaultTransactionManager()
        {
            TransactionManager = new QuorumTransactionManager(null, this);
        }
    }
}
