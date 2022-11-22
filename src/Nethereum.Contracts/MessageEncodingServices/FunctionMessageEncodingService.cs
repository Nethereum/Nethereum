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

        public byte[] GetCallDataHash(TContractFunction contractMessage)
        {
            return Util.Sha3Keccack.Current.CalculateHash(GetCallData(contractMessage));
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
            
            transactionInput.Type = contractMessage.GetHexTransactionType();
            transactionInput.MaxFeePerGas = contractMessage.GetHexMaxFeePerGas();
            transactionInput.MaxPriorityFeePerGas = contractMessage.GetMaxPriorityFeePerGas();
            transactionInput.AccessList = contractMessage.AccessList;   
            transactionInput.Nonce = contractMessage.GetHexNonce();
            return transactionInput;
        }

        public bool IsTransactionForFunction(Transaction transaction)
        {
            return FunctionBuilder.IsTransactionInputDataForFunction(transaction.Input);
        }

        public TContractFunction DecodeTransactionInput(TContractFunction contractMessageOutput, Transaction transaction)
        {
            if (!IsTransactionForFunction(transaction))
                throw new ArgumentException("The transaction given is not for the current function",
                    nameof(transaction));

            contractMessageOutput = DecodeInput(contractMessageOutput, transaction.Input);
            contractMessageOutput.Nonce = transaction.Nonce?.Value;
            contractMessageOutput.GasPrice = transaction.GasPrice?.Value;
            contractMessageOutput.AmountToSend = transaction.Value == null ? 0 : transaction.Value.Value;
            contractMessageOutput.Gas = transaction.Gas?.Value;
            contractMessageOutput.FromAddress = transaction.From;
            contractMessageOutput.MaxFeePerGas = transaction.MaxFeePerGas?.Value;
            contractMessageOutput.MaxPriorityFeePerGas = transaction.MaxPriorityFeePerGas?.Value;    

            if(transaction.Type == null)
            {
                contractMessageOutput.TransactionType = null;
            }
            else
            {
                contractMessageOutput.TransactionType = (byte)(transaction.Type.Value);
            }

            contractMessageOutput.AccessList = transaction.AccessList;
            
                 
            return contractMessageOutput;
        }

        public TReturn DecodeSimpleTypeOutput<TReturn>(string output)
        {
            return FunctionBuilder.DecodeTypeOutput<TReturn>(output);
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