using Nethereum.RPC.TransactionManagers;
using System;
using System.Collections.Generic;
using System.Text;

namespace Nethereum.RPC.Accounts
{
    public interface IAccount
    {
        string Address { get; }
        ITransactionManager TransactionManager { get; }
    }
}
