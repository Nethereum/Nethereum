using System;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.TransactionManagers;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3.Transactions
{
    public class SignedTransactionManager : ITransactionManager
    {
        private readonly IClient _rpcClient;
        private readonly string _privateKey;
        private readonly string _account;
        private TransactionSigner _transactionSigner;
        private readonly EthSendRawTransaction _ethSendTransaction;
        private readonly EthGetTransactionCount _ethGetTransactionCount;

        public SignedTransactionManager(IClient rpcClient, string privateKey, string account)
        {
            _rpcClient = rpcClient;
            _privateKey = privateKey;
            _account = account;
            _transactionSigner = new TransactionSigner();
            _ethSendTransaction = new EthSendRawTransaction(_rpcClient);
            _ethGetTransactionCount = new EthGetTransactionCount(_rpcClient);
        }

        public Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return SignAndSendTransaction(transactionInput);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            var nonce = transaction.Nonce;
            if (nonce == null)
                return await _ethGetTransactionCount.SendRequestAsync(_account).ConfigureAwait(false);
            return nonce;
        }

        private async Task<string> SignAndSendTransaction(TransactionInput transaction)
        {
            if (transaction.From != _account) throw new Exception("Invalid account used signing");

            var nonce = await GetNonceAsync(transaction);

            var gasPrice = transaction.GasPrice;
            if (gasPrice == null)
                gasPrice = new HexBigInteger(Nethereum.Core.Transaction.DEFAULT_GAS_PRICE);

            var gasLimit = transaction.Gas;
            if (gasLimit == null)
                gasLimit = new HexBigInteger(Nethereum.Core.Transaction.DEFFAULT_GAS_LIMIT);

            var value = transaction.Value;
            if (value == null)
                value = new HexBigInteger(0);

            var signedTransaction = _transactionSigner.SignTransaction(_privateKey, transaction.To, value.Value, nonce,
                gasPrice.Value, gasLimit.Value, transaction.Data);

            return await _ethSendTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }
    }
}