using System.Threading.Tasks;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public class Function : FunctionBase
    {
        public Function(Contract contract, FunctionABI function)
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

        public Task<TReturn> CallAsync<TReturn>(params object[] functionInput)
        {
            return base.CallAsync<TReturn>(CreateCallInput(functionInput));
        }

        public Task<TReturn> CallAsync<TReturn>(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            return base.CallAsync<TReturn>(CreateCallInput(from, gas, value, functionInput));
        }

        public Task<TReturn> CallAsync<TReturn>(string from, HexBigInteger gas,
           HexBigInteger value, BlockParameter block, params object[] functionInput)
        {
            return base.CallAsync<TReturn>(CreateCallInput(from, gas, value, functionInput), block);
        }

        public Task<TReturn> CallAsync<TReturn>(BlockParameter block, params object[] functionInput)
        {
            return base.CallAsync<TReturn>(CreateCallInput(functionInput), block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(params object[] functionInput)
            where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(functionInput));
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(from, gas, value, functionInput));
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(string from, HexBigInteger gas,
           HexBigInteger value, BlockParameter block, params object[] functionInput) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(from, gas, value, functionInput), block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(
             BlockParameter blockParameter, params object[] functionInput) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(functionInput), blockParameter);
        }

        public Task<HexBigInteger> EstimateGasAsync(params object[] functionInput)
        {
            return EstimateGasFromEncAsync(CreateCallInput(functionInput));
        }

        public Task<HexBigInteger> EstimateGasAsync(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        { 
            return EstimateGasFromEncAsync(CreateCallInput(from, gas, value,functionInput));
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

        public TransactionInput CreateTransactionInput(TransactionInput input, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, input);
        }

        public Task<string> SendTransactionAsync(string from, params object[] functionInput)
        {
            return base.SendTransactionAsync(CreateTransactionInput(from, functionInput));
        }

        public Task<string> SendTransactionAsync(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            return base.SendTransactionAsync(CreateTransactionInput(from, gas, value, functionInput));
        }

        public Task<string> SendTransactionAsync(TransactionInput input, params object[] functionInput)
        {
            return base.SendTransactionAsync(CreateTransactionInput(input, functionInput));
        }
    }

    public class Function<TFunctionInput> : FunctionBase
    {
        public Function(Contract contract, FunctionABI functionAbi)
            : base(contract, functionAbi)
        {
        }

        public CallInput CreateCallInputParameterless()
        {
            return CreateCallInput(FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature));
        }

        public Task<TReturn> CallAsync<TReturn>()
        {
            return base.CallAsync<TReturn>(CreateCallInputParameterless());
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput)
        {
            return base.CallAsync<TReturn>(CreateCallInput(functionInput));
        }

        private CallInput CreateCallInput(TFunctionInput functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateCallInput(encodedInput);
        }

        private CallInput CreateCallInput(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateCallInput(encodedInput, from, gas, value);
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {  
            return base.CallAsync<TReturn>(CreateCallInput(functionInput, from, gas, value));
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput,
             BlockParameter blockParameter)
        {
            return base.CallAsync<TReturn>(CreateCallInput(functionInput), blockParameter);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>() where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInputParameterless());
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(BlockParameter block) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInputParameterless(), block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(functionInput));
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, BlockParameter block) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(functionInput), block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, string from,
            HexBigInteger gas,
            HexBigInteger value) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(functionInput, from, gas, value));
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, string from,
           HexBigInteger gas,
           HexBigInteger value, BlockParameter block) where TReturn : new()
        {
            return base.CallAsync(new TReturn(), CreateCallInput(functionInput, from, gas, value), block);
        }

        public Task<HexBigInteger> EstimateGasAsync()
        {
            return EstimateGasFromEncAsync(CreateCallInputParameterless());
        }

        public Task<HexBigInteger> EstimateGasAsync(TFunctionInput functionInput)
        {
            return EstimateGasFromEncAsync(CreateCallInput(functionInput));
        }

        public Task<HexBigInteger> EstimateGasAsync(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return EstimateGasFromEncAsync(CreateCallInput(functionInput, from, gas, value));
        }

        public Task<HexBigInteger> EstimateGasAsync(TFunctionInput functionInput,
            CallInput callInput)
        {
            var encodedInput = GetData(functionInput);
            callInput.Data = encodedInput;
            return EstimateGasFromEncAsync(callInput);
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

        public TransactionInput CreateTransactionInput(TFunctionInput functionInput, string from, HexBigInteger gas, HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.CreateTransactionInput(encodedInput, from, gas, value);
        }

        public Task<string> SendTransactionAsync(TFunctionInput functionInput, string from)
        {
            return base.SendTransactionAsync(CreateTransactionInput(functionInput, from));
        }

        public Task<string> SendTransactionAsync(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return base.SendTransactionAsync(CreateTransactionInput(functionInput, from, gas, value));
        }

        public Task<string> SendTransactionAsync(TFunctionInput functionInput,
            TransactionInput input)
        {
            var encodedInput = GetData(functionInput);
            input.Data = encodedInput;
            return base.SendTransactionAsync(input);
        }
    }
}