using Nethereum.Signer;
using Transaction = Nethereum.Signer.Transaction;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Hex.HexTypes;
using System.Collections;
using System;
using System.Numerics;
using System.Text;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.Util;

namespace Nethereum.JsonRpc.UnityClient
{
    public class TransactionSignedUnityRequest:UnityRequest<string>
    {
        private string _url;
        private readonly string _privateKey;
        private readonly string _account;
        private readonly BigInteger? _chainId;
        private readonly TransactionSigner _transactionSigner;
        private readonly EthGetTransactionCountUnityRequest _transactionCountRequest;
        private readonly EthSendRawTransactionUnityRequest _ethSendTransactionRequest;
        private readonly EthEstimateGasUnityRequest _ethEstimateGasUnityRequest;
        private readonly EthGasPriceUnityRequest _ethGasPriceUnityRequest;
        public bool EstimateGas { get; set; } = true;
        

        public TransactionSignedUnityRequest(string url, string privateKey, BigInteger? chainId = null)
        {
            _chainId = chainId;
            _url = url;
            _account = EthECKey.GetPublicAddress(privateKey);
            _privateKey = privateKey;
            _transactionSigner = new TransactionSigner(); 
            _ethSendTransactionRequest = new EthSendRawTransactionUnityRequest(_url);
            _transactionCountRequest = new EthGetTransactionCountUnityRequest(_url);
            _ethEstimateGasUnityRequest = new EthEstimateGasUnityRequest(_url);
            _ethGasPriceUnityRequest = new EthGasPriceUnityRequest(_url);
        }

        public IEnumerator SignAndSendTransaction<TContractFunction>(TContractFunction function, string contractAdress) where TContractFunction : FunctionMessage
        {
            var transactionInput = function.CreateTransactionInput(contractAdress);
            yield return SignAndSendTransaction(transactionInput);
        }

        public IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>(TDeploymentMessage deploymentMessage)
            where TDeploymentMessage : ContractDeploymentMessage
        {
            var transactionInput = deploymentMessage.CreateTransactionInput();
            yield return SignAndSendTransaction(transactionInput);
        }

        public IEnumerator SignAndSendDeploymentContractTransaction<TDeploymentMessage>()
            where TDeploymentMessage : ContractDeploymentMessage, new()
        {
            var deploymentMessage = new TDeploymentMessage();
            yield return SignAndSendDeploymentContractTransaction(deploymentMessage);
        }

        public IEnumerator SignAndSendTransaction(TransactionInput transactionInput)
        {
            if (transactionInput == null) throw new ArgumentNullException("transactionInput");

            if (string.IsNullOrEmpty(transactionInput.From)) transactionInput.From = _account;

            if (!transactionInput.From.IsTheSameAddress(_account))
            {
                throw new Exception("Transaction Input From address does not match private keys address");
            }

            if (transactionInput.Gas == null)
            {
                if (EstimateGas)
                {
                    yield return _ethEstimateGasUnityRequest.SendRequest(transactionInput);

                    if (_ethEstimateGasUnityRequest.Exception == null)
                    {
                        var gas = _ethEstimateGasUnityRequest.Result;
                        transactionInput.Gas = gas;
                    }
                    else
                    {
                        this.Exception = _ethEstimateGasUnityRequest.Exception;
                        yield break;
                    }
                }
                else
                {
                    transactionInput.Gas = new HexBigInteger(Transaction.DEFAULT_GAS_LIMIT);
                }
            }

            if (transactionInput.GasPrice == null)
            {
                yield return _ethGasPriceUnityRequest.SendRequest();

                if (_ethGasPriceUnityRequest.Exception == null)
                {
                    var gasPrice = _ethGasPriceUnityRequest.Result;
                    transactionInput.GasPrice = gasPrice;
                }
                else
                {
                    this.Exception = _ethGasPriceUnityRequest.Exception;
                    yield break;
                }
            }

            var nonce = transactionInput.Nonce;
            
            if (nonce == null)
            {   
                yield return _transactionCountRequest.SendRequest(_account, Nethereum.RPC.Eth.DTOs.BlockParameter.CreateLatest());
            
                if(_transactionCountRequest.Exception == null) 
                {
                    nonce =  _transactionCountRequest.Result;
                }
                else
                {
                    this.Exception = _transactionCountRequest.Exception;
                    yield break;
                }
            }

            var value = transactionInput.Value;
            if (value == null)
                value = new HexBigInteger(0);

            string signedTransaction;

            if (_chainId == null)
            {
                signedTransaction = _transactionSigner.SignTransaction(_privateKey, transactionInput.To, value.Value, nonce,
                    transactionInput.GasPrice.Value, transactionInput.Gas.Value, transactionInput.Data);

            }
            else
            {
                signedTransaction = _transactionSigner.SignTransaction(_privateKey, _chainId.Value, transactionInput.To, value.Value, nonce,
                    transactionInput.GasPrice.Value, transactionInput.Gas.Value, transactionInput.Data);

            }
                 
            
            yield return _ethSendTransactionRequest.SendRequest(signedTransaction);
            
            if(_ethSendTransactionRequest.Exception == null) 
            {
                this.Result = _ethSendTransactionRequest.Result;
            }
            else
            {
                this.Exception = _ethSendTransactionRequest.Exception;
                yield break;
            }
        }
    }
}
