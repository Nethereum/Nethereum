using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.TransactionManagers;
using Nethereum.Signer;
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Web3.Accounts
{
    public class AccountSignerTransactionManager : TransactionManagerBase
    {
        private readonly string _privateKey;
        private readonly string _account;
        private readonly TransactionSigner _transactionSigner;
        private BigInteger _nonceCount = -1;
        public override BigInteger DefaultGasPrice { get; set; } = Transaction.DEFAULT_GAS_PRICE;
        public override BigInteger DefaultGas { get; set; } = Transaction.DEFAULT_GAS_LIMIT;

        public AccountSignerTransactionManager(IClient rpcClient, string privateKey)
        {
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            Client = rpcClient;
            _account = EthECKey.GetPublicAddress(privateKey);
            _privateKey = privateKey;
            _transactionSigner = new TransactionSigner();
        }

        public AccountSignerTransactionManager(string privateKey):this(null, privateKey)
        {
        }

        public override Task<string> SendTransactionAsync<T>(T transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return SignAndSendTransactionAsync(transactionInput);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            var ethGetTransactionCount = new EthGetTransactionCount(Client);
            var nonce = transaction.Nonce;
            if (nonce == null)
            {   
                //we are doing a check all the time on current nonce, we could just cache an increment but we might get out of sync.
                nonce = await ethGetTransactionCount.SendRequestAsync(_account).ConfigureAwait(false);
                if (nonce.Value <= _nonceCount)
                {
                    _nonceCount = _nonceCount + 1;
                    nonce = new HexBigInteger(_nonceCount);
                }
                else
                {
                    _nonceCount = nonce.Value;
                }
            }
            return nonce;
        }

        private async Task<string> SignAndSendTransactionAsync(TransactionInput transaction)
        {
            if(Client == null) throw new NullReferenceException("Client not configured");
            if (transaction == null) throw new ArgumentNullException(nameof(transaction));
            if (transaction.From.EnsureHexPrefix().ToLower() != _account.EnsureHexPrefix().ToLower()) throw new Exception("Invalid account used signing");
            SetDefaultGasPriceAndCostIfNotSet(transaction);

            var ethSendTransaction = new EthSendRawTransaction(Client);
            var nonce = await GetNonceAsync(transaction);

            var gasPrice = transaction.GasPrice;
            var gasLimit = transaction.Gas;
            
            var value = transaction.Value;
            if (value == null)
                value = new HexBigInteger(0);

            var signedTransaction = _transactionSigner.SignTransaction(_privateKey, transaction.To, value.Value, nonce,
                gasPrice.Value, gasLimit.Value, transaction.Data);

            return await ethSendTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }
    }
}