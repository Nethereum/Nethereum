using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.RPC.TransactionTypes;
using Nethereum.RPC.Chain;
using Nethereum.Model;
using System.Collections.Generic;
using Nethereum.Util;
using System.Linq;

namespace Nethereum.RPC.TransactionManagers
{
    public abstract class TransactionManagerBase : ITransactionManager
    {
        public virtual IClient Client { get; set; }
        public BigInteger DefaultGasPrice { get; set; } = -1; // Setting the default gas price to -1 as a flag
        public abstract BigInteger DefaultGas { get; set; }
        public IAccount Account { get; protected set; }
        public bool UseLegacyAsDefault { get; set; } = false;
        public bool CalculateOrSetDefaultGasPriceFeesIfNotSet { get; set; } = true;
        public bool EstimateOrSetDefaultGasIfNotSet { get; set; } = true;
        protected ChainFeature ChainFeature { get; set; }

        protected List<Authorisation> NextRequest7022Authorisations { get; set; } = new List<Authorisation>();

        public ITransactionVerificationAndRecovery TransactionVerificationAndRecovery { get; set; }

        public BigInteger? ChainId { get; protected set; }

        public bool IsTransactionToBeSendAsEIP1559(TransactionInput transaction)
        {
            if (ChainFeature != null && !ChainFeature.SupportEIP1559) return false;
            return (!UseLegacyAsDefault && transaction.GasPrice == null) || (transaction.MaxPriorityFeePerGas != null) || (transaction.Type != null && (transaction.Type.Value == TransactionTypes.TransactionType.EIP1559.AsByte() || transaction.Type.Value == TransactionTypes.TransactionType.EIP7702.AsByte()));
        }

#if !DOTNET35
        public BigInteger? DefaultMaxPriorityFeePerGas { get; set; } = null;

        private IFee1559SuggestionStrategy _fee1559SuggestionStrategy;
        public IFee1559SuggestionStrategy Fee1559SuggestionStrategy
        {
            get
            {
                if (_fee1559SuggestionStrategy == null)
                    _fee1559SuggestionStrategy = new TimePreferenceFeeSuggestionStrategy(Client);
                return _fee1559SuggestionStrategy;
            }
            set => _fee1559SuggestionStrategy = value;
        }

        public abstract Task<string> SignTransactionAsync(TransactionInput transaction);
        public abstract Task<Authorisation> SignAuthorisationAsync(Authorisation authorisation);

        protected void SetDefaultTransactionTypeIfNotSet(TransactionInput transaction)
        {
            if (IsTransactionToBeSendAsEIP1559(transaction))
            {
                if (transaction.Type == null)
                {

                    if (transaction.AuthorisationList != null)
                    {
                        transaction.Type = new HexBigInteger(TransactionTypes.TransactionType.EIP7702.AsByte());
                    }
                    else
                    {
                        transaction.Type = new HexBigInteger(TransactionTypes.TransactionType.EIP1559.AsByte());
                    }
                }
            }
        }

        protected async Task SetTransactionFeesOrPricingAsync(TransactionInput transaction)
        {
           
            if (CalculateOrSetDefaultGasPriceFeesIfNotSet)
            {
                await EnsureChainIdAndChainFeatureIsSetAsync().ConfigureAwait(false);

                if (IsTransactionToBeSendAsEIP1559(transaction))
                {
                    SetDefaultTransactionTypeIfNotSet(transaction);

                    if (transaction.MaxPriorityFeePerGas != null)
                    {
                        if (transaction.MaxFeePerGas == null)
                        {
                            var fee1559 = await CalculateFee1559Async(transaction.MaxPriorityFeePerGas.Value)
                                .ConfigureAwait(false);
                            transaction.MaxFeePerGas = new HexBigInteger(fee1559.MaxFeePerGas.Value);
                        }
                    }
                    else
                    {
                        var fee1559 = await CalculateFee1559Async().ConfigureAwait(false);
                        if (transaction.MaxFeePerGas == null)
                        {
                            transaction.MaxFeePerGas =
                                new HexBigInteger(fee1559.MaxFeePerGas.Value);

                            transaction.MaxPriorityFeePerGas =
                                new HexBigInteger(fee1559.MaxPriorityFeePerGas.Value);
                        }
                        else
                        {
                            if (transaction.MaxFeePerGas < fee1559.MaxPriorityFeePerGas)
                            {
                                transaction.MaxPriorityFeePerGas = transaction.MaxFeePerGas;
                            }
                            else
                            {
                                transaction.MaxPriorityFeePerGas =
                                    new HexBigInteger(fee1559.MaxPriorityFeePerGas.Value);
                            }
                        }
                    }
                }
                else
                {
                    if (transaction.GasPrice == null)
                    {
                        var gasPrice = await GetGasPriceAsync(transaction).ConfigureAwait(false);
                        transaction.GasPrice = gasPrice;
                    }
                }
            }
        }

        protected async Task EnsureChainIdAndChainFeatureIsSetAsync()
        {
            if(ChainId == null)
            {
                var ethGetChainId = new EthChainId(Client);
                try
                {
                    ChainId = await ethGetChainId.SendRequestAsync().ConfigureAwait(false);
                }
                catch
                {
                    ChainId = -1;
                }
                
            }

            if (ChainId != null)
            {
                ChainFeature = ChainFeaturesService.Current.GetChainFeature(ChainId.Value);
            }
        }

        public Task<string> SendRawTransactionAsync(string signedTransaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (string.IsNullOrEmpty(signedTransaction)) throw new ArgumentNullException(nameof(signedTransaction));
            var ethSendRawTransaction = new EthSendRawTransaction(Client);
            return ethSendRawTransaction.SendRequestAsync(signedTransaction);
        }

        private ITransactionReceiptService _transactionReceiptService;
        public ITransactionReceiptService TransactionReceiptService {
            get
            {
                if (_transactionReceiptService == null) return TransactionReceiptServiceFactory.GetDefaultransactionReceiptService(this);
                return _transactionReceiptService;
            }
            set
            {
                _transactionReceiptService = value;
            }
        }

       

        public Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(TransactionInput transactionInput, CancellationToken cancellationToken = default)
        {
            return TransactionReceiptService.SendRequestAndWaitForReceiptAsync(transactionInput, cancellationToken);
        }
               
        public virtual Task<HexBigInteger> EstimateGasAsync(CallInput callInput)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (callInput == null) throw new ArgumentNullException(nameof(callInput));
            var ethEstimateGas = new EthEstimateGas(Client);
            return ethEstimateGas.SendRequestAsync(callInput);
        }

        public abstract Task<string> SendTransactionAsync(TransactionInput transactionInput);
        
        public virtual Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount)
        {  
            return SendTransactionAsync(new TransactionInput() { From = from, To = to, Value = amount});
        }

        public Task<Fee1559> CalculateFee1559Async(BigInteger? maxPriorityFeePerGas = null)
        {
            if (maxPriorityFeePerGas == null) maxPriorityFeePerGas = DefaultMaxPriorityFeePerGas;
            if (Client == null) throw new NullReferenceException("Client not configured");
           
            return Fee1559SuggestionStrategy.SuggestFeeAsync(maxPriorityFeePerGas);
        }

        public async Task<HexBigInteger> GetGasPriceAsync(TransactionInput transactionInput)
        {
            if (transactionInput.GasPrice != null) return transactionInput.GasPrice;
            if (DefaultGasPrice >= 0) return new HexBigInteger(DefaultGasPrice);
            var ethGetGasPrice = new EthGasPrice(Client);
            return await ethGetGasPrice.SendRequestAsync().ConfigureAwait(false);
        }

        protected void SetDefaultGasPriceAndCostIfNotSet(TransactionInput transactionInput)
        {
            if (CalculateOrSetDefaultGasPriceFeesIfNotSet)
            {
                if (DefaultGasPrice != -1)
                {
                    if (transactionInput.GasPrice == null)
                        transactionInput.GasPrice = new HexBigInteger(DefaultGasPrice);
                }
            }

            if (DefaultGas != null && EstimateOrSetDefaultGasIfNotSet)
            {
                if (transactionInput.Gas == null) transactionInput.Gas = new HexBigInteger(DefaultGas);
            }
        }

        protected void SetDefaultGasIfNotSet(TransactionInput transactionInput)
        {
            if (DefaultGas != null && EstimateOrSetDefaultGasIfNotSet)
            {
                if (transactionInput.Gas == null) transactionInput.Gas = new HexBigInteger(DefaultGas);
            }

            
            if(transactionInput.Type != null && transactionInput.Type.Value == TransactionTypes.TransactionType.EIP7702.AsByte()
                || transactionInput.AuthorisationList != null && transactionInput.AuthorisationList.Count > 0)
            {
                //PER_AUTH_BASE_COST	12500
                //PER_EMPTY_ACCOUNT_COST  25000
                var gasCost = AuthorisationGasCalculator.CalculateGasForAuthorisationDelegation(transactionInput.AuthorisationList.ToArray());

                if (NextRequest7022Authorisations != null && NextRequest7022Authorisations.Count > 0)
                {
                   gasCost += AuthorisationGasCalculator.CalculateGasForAuthorisationDelegation(NextRequest7022Authorisations.ToArray());
                }

                transactionInput.Gas = new HexBigInteger(transactionInput.Gas.Value + gasCost );
            }
           
        }

        public async Task Add7022AuthorisationDelegationOnNextRequestAsync(string addressContract, bool useUniversalZeroChainId = false)
        {
            if (NextRequest7022Authorisations == null)
            {
                NextRequest7022Authorisations = new List<Authorisation>();
            }

            await EnsureChainIdAndChainFeatureIsSetAsync().ConfigureAwait(false);

            var authorisation = new Authorisation()
            {
                Address = addressContract,
                ChainId = useUniversalZeroChainId ? new HexBigInteger(0) : new HexBigInteger(ChainId.Value),
            };

            NextRequest7022Authorisations.Add(authorisation);
        }

        public void Remove7022AuthorisationDelegationOnNextRequest()
        {
            if (NextRequest7022Authorisations != null)
            {
                NextRequest7022Authorisations.Clear();
            }

            var authorisation = new Authorisation()
            {
                Address = AddressUtil.ZERO_ADDRESS,
                ChainId = new HexBigInteger(0),
            };

            NextRequest7022Authorisations.Add(authorisation);
        }

       

#endif
    }
}