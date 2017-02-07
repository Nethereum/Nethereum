using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.TransactionManagers;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Signer;
using Transaction = Nethereum.Signer.Transaction;

namespace Nethereum.Web3.Transactions
{
    public class SignedTransactionManager : ITransactionManager
    {
        private IClient _rpcClient;
        private readonly string _privateKey;
        private readonly string _account;
        private TransactionSigner _transactionSigner;
        private BigInteger _nonceCount = -1;

        public SignedTransactionManager(IClient rpcClient, string privateKey, string account)
        {
            if (privateKey == null) throw new ArgumentNullException(nameof(privateKey));
            if (account == null) throw new ArgumentNullException(nameof(account));
            _rpcClient = rpcClient;
            _privateKey = privateKey;
            _account = account;
            _transactionSigner = new TransactionSigner();
        }

        public SignedTransactionManager(string privateKey, string account):this(null, privateKey, account)
        {
        }

        public IClient Client
        {
            get { return _rpcClient; }
            set { _rpcClient = value; }
        }

        public Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput
        {
            if (transactionInput == null) throw new ArgumentNullException(nameof(transactionInput));
            return SignAndSendTransaction(transactionInput);
        }

        public async Task<HexBigInteger> GetNonceAsync(TransactionInput transaction)
        {
            if (Client == null) throw new NullReferenceException("Client not configured");
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

        private async Task<string> SignAndSendTransaction(TransactionInput transaction)
        {
            if(Client == null) throw new NullReferenceException("Client not configured");
            if (transaction.From.EnsureHexPrefix().ToLower() != _account.EnsureHexPrefix().ToLower()) throw new Exception("Invalid account used signing");
            var ethSendTransaction = new EthSendRawTransaction(Client);
            var nonce = await GetNonceAsync(transaction);

            var gasPrice = transaction.GasPrice;
            if (gasPrice == null)
                gasPrice = new HexBigInteger(Transaction.DEFAULT_GAS_PRICE);

            var gasLimit = transaction.Gas;
            if (gasLimit == null)
                gasLimit = new HexBigInteger(Transaction.DEFAULT_GAS_LIMIT);

            var value = transaction.Value;
            if (value == null)
                value = new HexBigInteger(0);

            var signedTransaction = _transactionSigner.SignTransaction(_privateKey, transaction.To, value.Value, nonce,
                gasPrice.Value, gasLimit.Value, transaction.Data);

            return await ethSendTransaction.SendRequestAsync(signedTransaction.EnsureHexPrefix()).ConfigureAwait(false);
        }
    }
}