using System;
using Nethereum.Contracts.CQS;
using Nethereum.Contracts.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.MessageEncodingServices
{
    public class FunctionMessageEncodingService<TContractFunction> :
        IContractMessageTransactionInputCreator<TContractFunction>,
        IFunctionMessageEncodingService<TContractFunction> where TContractFunction : ContractMessageBase
    {
        protected FunctionBuilder<TContractFunction> FunctionBuilder { get; set; }

        public string ContractAddress => FunctionBuilder.ContractAddress;

        public string DefaultAddressFrom { get; set; }

        public void SetContractAddress(string address)
        {
            FunctionBuilder.ContractAddress = address;
        }

        public FunctionMessageEncodingService(string contractAddress = null, string defaultAddressFrom = null)
        {
            FunctionBuilder = new FunctionBuilder<TContractFunction>(contractAddress);
            DefaultAddressFrom = defaultAddressFrom;
        }

        public byte[] GetCallData(TContractFunction contractMessage) 
        {
            return FunctionBuilder.GetDataAsBytes(contractMessage);
        }

        public CallInput CreateCallInput(TContractFunction contractMessage)
        {
            return FunctionBuilder.CreateCallInput(contractMessage,
                contractMessage.SetDefaultFromAddressIfNotSet(DefaultAddressFrom), contractMessage.GetHexMaximumGas(), contractMessage.GetHexValue());
        }

        public TransactionInput CreateTransactionInput(TContractFunction contractMessage)
        {
            var transactionInput = FunctionBuilder.CreateTransactionInput(contractMessage,
                contractMessage.SetDefaultFromAddressIfNotSet(DefaultAddressFrom),
                contractMessage.GetHexMaximumGas(),
                contractMessage.GetHexGasPrice(),
                contractMessage.GetHexValue());
            transactionInput.Nonce = contractMessage.GetHexNonce();
            return transactionInput;
        }

        public TContractFunction DecodeTransactionInput(TContractFunction contractMessageOuput, Transaction transactionInput)
        {
            contractMessageOuput = DecodeInput(contractMessageOuput, transactionInput.Input);
            contractMessageOuput.Nonce = transactionInput.Nonce?.Value;
            contractMessageOuput.GasPrice = transactionInput.GasPrice?.Value;
            contractMessageOuput.AmountToSend = transactionInput.Value == null ? 0 : transactionInput.Value.Value;
            contractMessageOuput.Gas = transactionInput.Gas?.Value;
            contractMessageOuput.FromAddress = transactionInput.From;
            return contractMessageOuput;
        }

        public TReturn DecodeSimpleTypeOutput<TReturn>(string output)
        {
            return FunctionBuilder.DecodeSimpleTypeOutput<TReturn>(output);
        }

        public TReturn DecodeDTOTypeOutput<TReturn>(string output) where TReturn : new()
        {
            return FunctionBuilder.DecodeDTOTypeOutput<TReturn>(output);
        }

        public TContractFunction DecodeInput(TContractFunction function, string data)
        {
            return FunctionBuilder.DecodeFunctionInput(function, data);
        }

    }
}