﻿using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.Fee1559Suggestions;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Model;

namespace Nethereum.RPC.TransactionManagers
{
    public interface ITransactionManager
    {
        IClient Client { get; set; }
        BigInteger DefaultGasPrice { get; set; }
        BigInteger DefaultGas { get; set; }
        IAccount Account { get; }
        bool UseLegacyAsDefault { get; set; }
        ITransactionVerificationAndRecovery TransactionVerificationAndRecovery { get; set; }
#if !DOTNET35
        IFee1559SuggestionStrategy Fee1559SuggestionStrategy { get; set; }

        Task<string> SendTransactionAsync(TransactionInput transactionInput);
        Task<HexBigInteger> EstimateGasAsync(CallInput callInput);
        Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount);
        Task<string> SignTransactionAsync(TransactionInput transaction);
        ITransactionReceiptService TransactionReceiptService { get; set; }
        bool CalculateOrSetDefaultGasPriceFeesIfNotSet { get; set; }
        bool EstimateOrSetDefaultGasIfNotSet { get; set; }
        BigInteger? ChainId { get; }

        Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(TransactionInput transactionInput, CancellationToken cancellationToken = default);
        Task<Authorisation> SignAuthorisationAsync(Authorisation authorisation);
        Task Add7022AuthorisationDelegationOnNextRequestAsync(string addressContract, bool useUniversalZeroChainId = false);
        void Remove7022AuthorisationDelegationOnNextRequest();

#endif

    }
}
