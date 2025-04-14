using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;

namespace Nethereum.Accounts
{
    public class EIP7022SponsorAuthorisationService
    {
        private const int DefaultTopUpGas = 1000;
        private readonly Authorisation7702Signer _authorisation7702Signer;
        private readonly ITransactionManager transactionManager;
        private readonly IEthApiService ethApiService;

        public BigInteger? ChainId { get; protected set; }

        public EIP7022SponsorAuthorisationService(ITransactionManager transactionManager, IEthApiService ethApiService)
        {
           
            ChainId = transactionManager.ChainId;
            _authorisation7702Signer = new Authorisation7702Signer();
            this.transactionManager = transactionManager;
            this.ethApiService = ethApiService;
        }

        private TransactionInput CreateDefaultTransactionInputToAuthoriseMultipleAddresses(int topUpGas = DefaultTopUpGas)
        {
            return new TransactionInput()
            {
                From = transactionManager.Account.Address,
                Gas =
                new HexBigInteger(transactionManager.DefaultGas + topUpGas), //the rest is handled by the transaction manager
                To = transactionManager.Account.Address,
                Value = new HexBigInteger(0),
                Data = "0x"
            };
        }

       

        protected async Task EnsureChainIdAndChainFeatureIsSetAsync()
        {
            if (ChainId == null)
            {
                var ethGetChainId = new EthChainId(ethApiService.Client);
                try
                {
                    ChainId = await ethGetChainId.SendRequestAsync().ConfigureAwait(false);
                }
                catch
                {
                    ChainId = -1;
                }

            }
        }

        private async Task<BigInteger> GetNonceAsync(string address)
        {
            var ethGetTransactionCount = new EthGetTransactionCount(ethApiService.Client);
            var nonce = await ethGetTransactionCount.SendRequestAsync(address, BlockParameter.CreatePending()).ConfigureAwait(false);
            return nonce;
        }

        public async Task<Authorisation> SignSponsoredAuthorisationAsync(EthECKey privateKeySponsoredAccount, string contractAddress, bool useUniversalZeroChainId = false, bool brandNewAccount = false)
        {
            await EnsureChainIdAndChainFeatureIsSetAsync();

            var address = privateKeySponsoredAccount.GetPublicAddress();
            BigInteger nonce = 0;
            if (!brandNewAccount)
            {
                nonce = await GetNonceAsync(address).ConfigureAwait(false);
            }

            var authorisation = new Authorisation7702
            {
                Address = contractAddress,
                ChainId = useUniversalZeroChainId ? new HexBigInteger(0) : new HexBigInteger(ChainId.Value),
                Nonce = nonce,
            };

            var authorisationSigned = _authorisation7702Signer.SignAuthorisation(privateKeySponsoredAccount, authorisation);
            return authorisationSigned.ToRPCAuthorisation();
        }

        public async Task<string> AuthoriseSponsoredRequestAsync(EthECKey privateKeySponsoredAccount, string contractAddress, int topUpGas = DefaultTopUpGas, bool useUniversalZeroChainId = false, bool brandNewAccount = false)
        {
            TransactionInput transactionInput = await SignAndCreateAuthoriseSponsoredTransactionInput(privateKeySponsoredAccount, contractAddress, topUpGas, useUniversalZeroChainId, brandNewAccount).ConfigureAwait(false);
            var transactionHash = await transactionManager.SendTransactionAsync(transactionInput).ConfigureAwait(false);
            return transactionHash;
        }

        public async Task<TransactionInput> SignAndCreateAuthoriseSponsoredTransactionInput(EthECKey privateKeySponsoredAccount, string contractAddress, int topUpGas, bool useUniversalZeroChainId, bool brandNewAccount)
        {
            var authorisation = await SignSponsoredAuthorisationAsync(privateKeySponsoredAccount, contractAddress, useUniversalZeroChainId, brandNewAccount).ConfigureAwait(false);
            var transactionInput = CreateDefaultTransactionInputToAuthoriseMultipleAddresses(topUpGas);
            transactionInput.AuthorisationList.Add(authorisation);
            return transactionInput;
        }

        public async Task<TransactionReceipt> AuthoriseBatchSponsoredRequestAndWaitForReceiptAsync(EthECKey[] privateKeySponsoredAccounts, string contractAddress, int topUpGas = DefaultTopUpGas, bool useUniversalZeroChainId = false, bool brandNewAccount = false)
        {
            TransactionInput transactionInput = await SignAndCreateAuthoriseBatchSponsoredTransactionInput(privateKeySponsoredAccounts, contractAddress, topUpGas, useUniversalZeroChainId, brandNewAccount).ConfigureAwait(false);
            var transactionHash = await transactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput).ConfigureAwait(false);
            return transactionHash;
        }

        public async Task<string> AuthoriseBatchSponsoredRequestAsync(EthECKey[] privateKeySponsoredAccounts, string contractAddress, int topUpGas = DefaultTopUpGas, bool useUniversalZeroChainId = false, bool brandNewAccount = false)
        {
            TransactionInput transactionInput = await SignAndCreateAuthoriseBatchSponsoredTransactionInput(privateKeySponsoredAccounts, contractAddress, topUpGas, useUniversalZeroChainId, brandNewAccount).ConfigureAwait(false);
            var transactionHash = await transactionManager.SendTransactionAsync(transactionInput).ConfigureAwait(false);
            return transactionHash;
        }

        private async Task<TransactionInput> SignAndCreateAuthoriseBatchSponsoredTransactionInput(EthECKey[] privateKeySponsoredAccounts, string contractAddress, int topUpGas, bool useUniversalZeroChainId, bool brandNewAccount)
        {
            var authorisations = new List<Authorisation>();
            foreach (var signer in privateKeySponsoredAccounts)
            {
                var authorisation = await SignSponsoredAuthorisationAsync(signer, contractAddress, useUniversalZeroChainId, brandNewAccount).ConfigureAwait(false);
                authorisations.Add(authorisation);
            }

            var transactionInput = CreateDefaultTransactionInputToAuthoriseMultipleAddresses(topUpGas);
            transactionInput.AuthorisationList = authorisations;
            return transactionInput;
        }

        public async Task<TransactionReceipt> AuthoriseSponsoredRequestAndWaitForReceiptAsync(EthECKey privateKeySponsoredAccount, string contractAddress, int topUpGas = DefaultTopUpGas, bool useUniversalZeroChainId = false, bool brandNewAccount = false)
        {
            var transactionInput = await SignAndCreateAuthoriseSponsoredTransactionInput(privateKeySponsoredAccount, contractAddress, topUpGas, useUniversalZeroChainId, brandNewAccount).ConfigureAwait(false);
            return await transactionManager.SendTransactionAndWaitForReceiptAsync(transactionInput).ConfigureAwait(false);
        }

    }
}
