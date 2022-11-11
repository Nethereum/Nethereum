using Nethereum.Signer;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.Hex.HexTypes;
using System.Collections;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Text;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Blocks;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Newtonsoft.Json;
using Nethereum.Unity.Contracts;
using Nethereum.Unity.FeeSuggestions;

namespace Nethereum.Unity.Rpc
{
    public class TransactionSignedUnityRequest : UnityRequest<string>, IContractTransactionUnityRequest
    {
        private string _url;
        private readonly string _privateKey;
        private readonly string _account;
        private readonly BigInteger? _chainId;
        private readonly LegacyTransactionSigner _transactionSigner;
        private readonly EthGetTransactionCountUnityRequest _transactionCountRequest;
        private readonly EthSendRawTransactionUnityRequest _ethSendTransactionRequest;
        private readonly EthEstimateGasUnityRequest _ethEstimateGasUnityRequest;
        private readonly EthGasPriceUnityRequest _ethGasPriceUnityRequest;
        public IFee1559SuggestionUnityRequestStrategy Fee1559SuggestionStrategy { get; set; }
        public bool EstimateGas { get; set; } = true;
        public bool UseLegacyAsDefault { get; set; } = false;

        public TransactionSignedUnityRequest(string url, string privateKey, BigInteger? chainId = null, JsonSerializerSettings jsonSerializerSettings = null, Dictionary<string, string> requestHeaders = null)
        {
            _chainId = chainId;
            _url = url;
            _account = EthECKey.GetPublicAddress(privateKey);
            _privateKey = privateKey;
            _transactionSigner = new LegacyTransactionSigner();
            _ethSendTransactionRequest = new EthSendRawTransactionUnityRequest(_url, jsonSerializerSettings, requestHeaders);
            _transactionCountRequest = new EthGetTransactionCountUnityRequest(_url, jsonSerializerSettings, requestHeaders);
            _ethEstimateGasUnityRequest = new EthEstimateGasUnityRequest(_url, jsonSerializerSettings, requestHeaders);
            _ethGasPriceUnityRequest = new EthGasPriceUnityRequest(_url, jsonSerializerSettings, requestHeaders);
            Fee1559SuggestionStrategy = new SimpleFeeSuggestionUnityRequestStrategy(url, jsonSerializerSettings, requestHeaders);
        }

        public TransactionSignedUnityRequest(string privateKey, BigInteger chainId, IUnityRpcRequestClientFactory unityRpcClientFactory)
        {
            _chainId = chainId;
            _account = EthECKey.GetPublicAddress(privateKey);
            _privateKey = privateKey;
            _transactionSigner = new LegacyTransactionSigner();
            _ethSendTransactionRequest = new EthSendRawTransactionUnityRequest(unityRpcClientFactory);
            _transactionCountRequest = new EthGetTransactionCountUnityRequest(unityRpcClientFactory);
            _ethEstimateGasUnityRequest = new EthEstimateGasUnityRequest(unityRpcClientFactory);
            _ethGasPriceUnityRequest = new EthGasPriceUnityRequest(unityRpcClientFactory);
            Fee1559SuggestionStrategy = new SimpleFeeSuggestionUnityRequestStrategy(unityRpcClientFactory);
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
                        Exception = _ethEstimateGasUnityRequest.Exception;
                        yield break;
                    }
                }
                else
                {
                    transactionInput.Gas = new HexBigInteger(SignedLegacyTransaction.DEFAULT_GAS_LIMIT);
                }
            }

            if (IsTransactionToBeSendAsEIP1559(transactionInput))
            {

                transactionInput.Type = new HexBigInteger(TransactionType.EIP1559.AsByte());
                if (transactionInput.MaxPriorityFeePerGas != null)
                {
                    if (transactionInput.MaxFeePerGas == null)
                    {
                        yield return Fee1559SuggestionStrategy.SuggestFee(transactionInput.MaxPriorityFeePerGas.Value);
                        if (Fee1559SuggestionStrategy.Exception == null)
                        {
                            transactionInput.MaxFeePerGas = new HexBigInteger(Fee1559SuggestionStrategy.Result.MaxFeePerGas.Value);
                        }
                        else
                        {
                            Exception = Fee1559SuggestionStrategy.Exception;
                            yield break;
                        }
                    }
                }
                else
                {

                    yield return Fee1559SuggestionStrategy.SuggestFee();
                    if (Fee1559SuggestionStrategy.Exception == null)
                    {
                        if (transactionInput.MaxFeePerGas == null)
                        {
                            transactionInput.MaxFeePerGas =
                                new HexBigInteger(Fee1559SuggestionStrategy.Result.MaxFeePerGas.Value);

                            transactionInput.MaxPriorityFeePerGas =
                                new HexBigInteger(Fee1559SuggestionStrategy.Result.MaxPriorityFeePerGas.Value);
                        }
                        else
                        {
                            if (transactionInput.MaxFeePerGas < Fee1559SuggestionStrategy.Result.MaxPriorityFeePerGas)
                            {
                                transactionInput.MaxPriorityFeePerGas = transactionInput.MaxFeePerGas;
                            }
                            else
                            {
                                transactionInput.MaxPriorityFeePerGas = new HexBigInteger(Fee1559SuggestionStrategy.Result.MaxPriorityFeePerGas.Value);
                            }
                        }

                    }
                    else
                    {
                        Exception = Fee1559SuggestionStrategy.Exception;
                        yield break;
                    }
                }
            }
            else
            {
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
                        Exception = _ethGasPriceUnityRequest.Exception;
                        yield break;
                    }
                }
            }


            var nonce = transactionInput.Nonce;

            if (nonce == null)
            {
                yield return _transactionCountRequest.SendRequest(_account, BlockParameter.CreateLatest());

                if (_transactionCountRequest.Exception == null)
                {
                    nonce = _transactionCountRequest.Result;
                }
                else
                {
                    Exception = _transactionCountRequest.Exception;
                    yield break;
                }
            }

            var value = transactionInput.Value;
            if (value == null)
                value = new HexBigInteger(0);

            string signedTransaction;
            if (IsTransactionToBeSendAsEIP1559(transactionInput))
            {

                var transaction1559 = new Transaction1559(_chainId.Value, nonce, transactionInput.MaxPriorityFeePerGas.Value, transactionInput.MaxFeePerGas.Value,
                    transactionInput.Gas.Value, transactionInput.To, value.Value, transactionInput.Data,
                    null);
                transaction1559.Sign(new EthECKey(_privateKey));
                signedTransaction = transaction1559.GetRLPEncoded().ToHex();
            }
            else
            {
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
            }

            yield return _ethSendTransactionRequest.SendRequest(signedTransaction);

            if (_ethSendTransactionRequest.Exception == null)
            {
                Result = _ethSendTransactionRequest.Result;
            }
            else
            {
                Exception = _ethSendTransactionRequest.Exception;
                yield break;
            }
        }

        public bool IsTransactionToBeSendAsEIP1559(TransactionInput transaction)
        {
            return !UseLegacyAsDefault && transaction.GasPrice == null || transaction.MaxPriorityFeePerGas != null || transaction.Type != null && transaction.Type.Value == TransactionType.EIP1559.AsByte();
        }
    }
}
