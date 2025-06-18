using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.AccountSigning;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Signer;

namespace Nethereum.Accounts.ViewOnly
{
    public class ViewOnlyAccount : IAccount
    {
        public ViewOnlyAccount(string accountAddress)
        {
            Address = accountAddress;
            InitialiseDefaultTransactionManager();
        }

        public ViewOnlyAccount(string accountAddress,
            ViewOnlyAccountTransactionManager transactionManager)
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
            TransactionManager = new ViewOnlyAccountTransactionManager(null, this);
        }
    }
}
