using System.Runtime.InteropServices.ComTypes;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionTypes;

namespace Nethereum.Contracts
{
    public class FunctionBuilder : FunctionBuilderBase
    {

        public FunctionBuilder(string contractAddress, FunctionABI function)
            : base(contractAddress, function)
        {
        }

        public CallInput CreateCallInput(params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateCallInput(encodedInput);
        }

        public CallInput CreateCallInput(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateCallInput(encodedInput, from, gas, value);
        }

        public string GetData(params object[] functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature, FunctionABI.InputParameters,
                functionInput);
        }

        public TransactionInput CreateTransactionInput(string from, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from, null, null);
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from, gas, value);
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas, HexBigInteger gasPrice,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from, gas, gasPrice, value);
        }

        public TransactionInput CreateTransactionInput(HexBigInteger type, string from, HexBigInteger gas,
            HexBigInteger value, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(type, encodedInput, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas,
            HexBigInteger value, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(TransactionType.EIP1559.AsHexBigInteger(), encodedInput, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
        }

        public TransactionInput CreateTransactionInput(TransactionInput input, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, input);
        }
    }


    public class FunctionBuilder<TFunctionInput> : FunctionBuilderBase
    {
        public FunctionBuilder(string contractAddres):base(contractAddres)
        {

            FunctionABI = ABITypedRegistry.GetFunctionABI<TFunctionInput>();
        }

        public FunctionBuilder(string contractAddress, FunctionABI function)
            : base(contractAddress, function)
        {
        }

        public CallInput CreateCallInputParameterless()
        {
            return CreateCallInput(FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature));
        }

        public CallInput CreateCallInput(TFunctionInput functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateCallInput(encodedInput);
        }

        public CallInput CreateCallInput(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateCallInput(encodedInput, from, gas, value);
        }

        public string GetData(TFunctionInput functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(functionInput, FunctionABI.Sha3Signature);
        }

        public byte[] GetDataAsBytes(TFunctionInput functionInput)
        {
            return GetData(functionInput).HexToByteArray();
        }

        public TFunctionInput DecodeFunctionInput(TFunctionInput functionInput, TransactionInput transactionInput)
        {
            return FunctionCallDecoder.DecodeFunctionInput<TFunctionInput>(functionInput, FunctionABI.Sha3Signature, transactionInput.Data);
        }

        public TFunctionInput DecodeFunctionInput(TFunctionInput functionInput, string data)
        {
            return FunctionCallDecoder.DecodeFunctionInput<TFunctionInput>(functionInput, FunctionABI.Sha3Signature, data);
        }

        public TransactionInput CreateTransactionInput(TFunctionInput functionInput, string from)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from);
        }

        public TransactionInput CreateTransactionInput(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from, gas, value);
        }

        public TransactionInput CreateTransactionInput(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger gasPrice, HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from, gas, gasPrice, value);
        }


        public TransactionInput CreateTransactionInput(TFunctionInput functionInput,  HexBigInteger type, string from, HexBigInteger gas,
            HexBigInteger value, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(type, encodedInput, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
        }

        public TransactionInput CreateTransactionInput(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(TransactionType.EIP1559.AsHexBigInteger(), encodedInput, from, gas, value, maxFeePerGas, maxPriorityFeePerGas);
        }
    }
}