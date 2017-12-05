using Nethereum.RPC.TransactionManagers;
using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.RPC.NonceServices;

namespace Nethereum.RPC.Accounts
{
    public interface IAccount
    {
        string Address { get; }
        ITransactionManager TransactionManager { get; }

        INonceService NonceService { get; set; }
    }
}
