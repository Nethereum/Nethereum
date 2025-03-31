using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.NonceServices;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.RPC.Eth.Mappers;
using System.Collections.Generic;

namespace Nethereum.Web3.Accounts
{
    public class AccountSignerTransactionManager : TransactionManagerBase
    {
        private readonly AccountOfflineTransactionSigner _transactionSigner;
        private readonly Authorisation7702Signer _authorisation7702Signer;


        public AccountSignerTransactionManager(IClient rpcClient, Account account, BigInteger? overridingAccountChainId = null)
        {
            if (overridingAccountChainId == null)
            {
                ChainId = account.ChainId;
            }
            else
            {
                ChainId = overridingAccountChainId;
            }
            
            Account = account ?? throw new ArgumentNullException(nameof(account));
            Client = rpcClient;
            _transactionSigner = new AccountOfflineTransactionSigner();
            _authorisation7702Signer = new Authorisation7702Signer();
        }


        public AccountSignerTransactionManager(IClient rpcClient, string privateKey, BigInteger? chainId = null)
        {
            ChainId = chainId;
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            Client = rpcClient;
            Account = new Account(privateKey, chainId);
            Account.NonceService = new InMemoryNonceService(Account.Address, rpcClient);
            _transactionSigner = new AccountOfflineTransactionSigner();
            _authorisation7702Signer = new Authorisation7702Signer();
        }

        public AccountSignerTransactionManager(string privateKey, BigInteger? chainId = null) : this(null, privateKey,
            chainId)
        {
        }

       

        public override BigInteger DefaultGas { get; set; } = SignedLegacyTransaction.DEFAULT_GAS_LIMIT;


        public async override Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            await EnsureChainIdAndChainFeatureIsSetAsync().ConfigureAwait(false);
            return await SignAndSendTransactionAsync(transactionInput).ConfigureAwait(false);
        }

        public async override Task<string> SignTransactionAsync(TransactionInput transaction)
        {
            await EnsureChainIdAndChainFeatureIsSetAsync().ConfigureAwait(false);
            return await SignTransactionRetrievingNextNonceAsync(transaction).ConfigureAwait(false);
        }

        public string SignTransaction(TransactionInput transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            SetDefaultGasIfNotSet(transaction);

            return _transactionSigner.SignTransaction((Account) Account, transaction, ChainId);
        }

        protected async Task<string> SignTransactionRetrievingNextNonceAsync(TransactionInput transaction)
        {
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!transaction.From.IsTheSameAddress(Account.Address))
                throw new Exception("Invalid account used signing");

            var nonce = await GetNonceAsync(transaction).ConfigureAwait(false);
            transaction.Nonce = nonce;

            //authorisations happen after nonce is retrieved and set

            if (NextRequest7022Authorisations != null && NextRequest7022Authorisations.Count > 0)
            {
                if(transaction.AuthorisationList == null)
                {
                    transaction.AuthorisationList = new List<Authorisation>();
                }
                transaction.AuthorisationList.AddRange(NextRequest7022Authorisations);
                NextRequest7022Authorisations.Clear();
            }

            if (transaction.AuthorisationList != null)
            {
                var newAuthorisationList = new List<Authorisation>();
                foreach (var authorisation in transaction.AuthorisationList)
                {
                    if (authorisation.Nonce == null && authorisation.YParity == null) // not signed already
                    {
                        newAuthorisationList.Add(await SignAuthorisationAsync(authorisation).ConfigureAwait(false));
                    }
                    else
                    {
                        newAuthorisationList.Add(authorisation);
                    }
                }

                transaction.AuthorisationList = newAuthorisationList;
            }
            await SetTransactionFeesOrPricingAsync(transaction).ConfigureAwait(false);

            return SignTransaction(transaction);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var nonce = transaction.Nonce;
            if (nonce == null)
            {
                if (Account.NonceService == null)
                    Account.NonceService = new InMemoryNonceService(Account.Address, Client);
                Account.NonceService.Client = Client;
                nonce = await Account.NonceService.GetNextNonceAsync().ConfigureAwait(false);
            }

            return nonce;
        }

        private async Task<string> SignAndSendTransactionAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (!transaction.From.IsTheSameAddress(Account.Address))
                throw new Exception("Invalid account used signing");

            var ethSendTransaction = new EthSendRawTransaction(Client);
            var signedTransaction = await SignTransactionRetrievingNextNonceAsync(transaction).ConfigureAwait(false);
            return await ethSendTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }

      
        public override async Task<Authorisation> SignAuthorisationAsync(Authorisation authorisation)
        {
            if (authorisation == null) throw new ArgumentNullException(nameof(authorisation));
            if (authorisation.ChainId == null)
            {
                await EnsureChainIdAndChainFeatureIsSetAsync();
                authorisation.ChainId = new HexBigInteger(ChainId.Value);
            } 
            if (authorisation.Address == null) throw new ArgumentNullException(nameof(authorisation.Address));
            if (authorisation.Nonce == null)
            {
                authorisation.Nonce = await GetNonceAsync(new TransactionInput()).ConfigureAwait(false);
            }
            var authorisation7702 = new Authorisation7702
            {
                Address = authorisation.Address,
                ChainId = authorisation.ChainId.Value,
                Nonce = authorisation.Nonce.Value,
            };

            var authorisationSigned = _authorisation7702Signer.SignAuthorisation(((Account)Account).PrivateKey, authorisation7702);
            return authorisationSigned.ToRPCAuthorisation();
        }
    }
}