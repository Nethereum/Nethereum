using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public class FunctionBuilder : FunctionBuilderBase
    {
        public FunctionBuilder(ContractBuilder contract, FunctionABI function)
            : base(contract, function)
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


        public TransactionInput CreateTransactionInput(TransactionInput input, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, input);
        }
    }

    public class FunctionBuilder<TFunctionInput> : FunctionBuilderBase
    {
        public FunctionBuilder(ContractBuilder contract, FunctionABI function)
            : base(contract, function)
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
    }
}