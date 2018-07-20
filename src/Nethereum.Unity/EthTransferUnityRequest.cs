using System.Collections;
using System.Numerics;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.JsonRpc.UnityClient
{
    public class EthTransferUnityRequest : UnityRequest<string>
    {
        private readonly string _account;
        private TransactionSignedUnityRequest _transactionSignedUnityRequest;

        public EthTransferUnityRequest(string url, string privateKey, string account)
        {
            _account = account;
            _transactionSignedUnityRequest = new TransactionSignedUnityRequest(url, privateKey, account);
        }

        public IEnumerator TransferEther(string toAddress, decimal etherAmount, decimal? gasPriceGwei = null, BigInteger? gas = null)
        {
            var transactionInput =
                EtherTransferTransactionInputBuilder.CreateTransactionInput(_account, toAddress, etherAmount,
                    gasPriceGwei, gas);
            yield return  _transactionSignedUnityRequest.SignAndSendTransaction(transactionInput);

            if (_transactionSignedUnityRequest.Exception == null)
            {

                this.Result = _transactionSignedUnityRequest.Result;
            }
            else
            {
                this.Exception = _transactionSignedUnityRequest.Exception;
            }
        }
    }
}