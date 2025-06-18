using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;
using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Signer;

namespace Nethereum.Accounts.ViewOnly
{
    public class ViewOnlyAccountTransactionManager : TransactionManagerBase
    {
        public ViewOnlyAccountTransactionManager(IClient client, ViewOnlyAccount account)
        {
            Account = account;
            Client = client;
            TransactionVerificationAndRecovery = new TransactionVerificationAndRecoveryImp();
        }

        public ViewOnlyAccountTransactionManager(IClient client, string accountAddress)
        {
            Account = new ViewOnlyAccount(accountAddress, this);
            Client = client;
            TransactionVerificationAndRecovery = new TransactionVerificationAndRecoveryImp();
        }

        public ViewOnlyAccountTransactionManager(string accountAddress) : this(null, accountAddress
            )
        {
        }

        public override BigInteger DefaultGas { get; set; } = SignedLegacyTransaction.DEFAULT_GAS_LIMIT;

        public void SetAccount(ViewOnlyAccount account)
        {
            Account = account;
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var nonce = transaction.Nonce;
            if (nonce == null)
                if (Account.NonceService != null)
                {
                    Account.NonceService.Client = Client;
                    nonce = await Account.NonceService.GetNextNonceAsync().ConfigureAwait(false);
                }

            return nonce;
        }


        public override async Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            throw new InvalidOperationException("ViewOnly accounts cannot send transactions");
        }

        public override Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {
            throw new InvalidOperationException("ViewOnly accounts cannot send transactions");
        }

        public override Task<string> SignTransactionAsync(TransactionInput transaction)
        {
            throw new InvalidOperationException("ViewOnly accounts cannot sign offline transactions");
        }

        public override Task<Authorisation> SignAuthorisationAsync(Authorisation authorisation)
        {
            throw new InvalidOperationException("ViewOnly accounts cannot sign authorisations");
        }
    }
}
