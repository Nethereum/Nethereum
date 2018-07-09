using System;
using Nethereum.Contracts.Services;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.ContractHandlers
{
#if !DOTNET35
    public abstract class ContractTransactionHandlerBase
    {
        protected IClient Client { get; }
        protected IAccount Account { get; }
        protected ITransactionManager TransactionManager { get; }

        protected ContractTransactionHandlerBase(IClient client, IAccount account)
        {
            Client = client;
            Account = account;
            if(account.TransactionManager == null) throw new ArgumentException("Transaction manager not initialised", nameof(account));
            //link client to transaction manager
            Account.TransactionManager.Client = client;
            TransactionManager = Account.TransactionManager;
        }

        protected ContractTransactionHandlerBase(ITransactionManager transactionManager)
        {
            Client = transactionManager.Client;
            Account = transactionManager.Account;
            TransactionManager = transactionManager;
        }

        public virtual string GetAccountAddressFrom()
        {
            return Account?.Address;
        }
    }
#endif
}