using System.Collections.Generic;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public abstract class FunctionBuilderBase
    {
        protected FunctionBuilderBase(string contractAddress, FunctionABI functionAbi):this(contractAddress)
        {
            FunctionABI = functionAbi;   
        }

        protected FunctionBuilderBase(string contractAddress)
        {
            ContractAddress = contractAddress;
            FunctionCallDecoder = new FunctionCallDecoder();
            FunctionCallEncoder = new FunctionCallEncoder();
        }
    

        public string ContractAddress { get; set; }

        protected FunctionCallDecoder FunctionCallDecoder { get; set; }

        protected FunctionCallEncoder FunctionCallEncoder { get; set; }

        public FunctionABI FunctionABI { get; protected set; }

        public bool IsTransactionInputDataForFunction(string data)
        {
            return FunctionCallDecoder.IsDataForFunction(FunctionABI.Sha3Signature, data);
        }

        public List<ParameterOutput> DecodeInput(string data)
        {
            return FunctionCallDecoder.DecodeFunctionInput(FunctionABI.Sha3Signature, data,
                FunctionABI.InputParameters);
        }

        public List<ParameterOutput> DecodeOutput(string data)
        {
            return FunctionCallDecoder.DecodeDefaultData(data,
                FunctionABI.OutputParameters);
        }
        public TReturn DecodeSimpleTypeOutput<TReturn>(string output)
        {
            return FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                GetFirstParameterOrNull(FunctionABI.OutputParameters), output);
        }

        public TReturn DecodeDTOTypeOutput<TReturn>(TReturn functionOuput, string output)
        {
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, output);
        }

        public TReturn DecodeDTOTypeOutput<TReturn>(string output) where TReturn : new()
        {
            return FunctionCallDecoder.DecodeFunctionOutput<TReturn>(output);
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return new TransactionInput(encodedInput, from, gas, value);
        }

        protected CallInput CreateCallInput(string encodedFunctionCall)
        {
            return new CallInput(encodedFunctionCall, ContractAddress);
        }

        protected CallInput CreateCallInput(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return new CallInput(encodedFunctionCall, ContractAddress, from, gas, value);
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall, string from)
        {
            var tx = new TransactionInput(encodedFunctionCall, ContractAddress) {From = from};
            return tx;
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return new TransactionInput(encodedFunctionCall, ContractAddress, from, gas, value);
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger gasPrice,
            HexBigInteger value)
        {
            return new TransactionInput(encodedFunctionCall, ContractAddress, from, gas, gasPrice, value);
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall,
            TransactionInput input)
        {
            input.Data = encodedFunctionCall;
            input.To = ContractAddress;
            return input;
        }

        private Parameter GetFirstParameterOrNull(Parameter[] parameters)
        {
            if (parameters == null) return null;
            if (parameters.Length == 0) return null;
            return parameters[0];
        }
    }
}