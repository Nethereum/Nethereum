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

        public Task<TReturn> CallAsync<TReturn>(params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput);
        }

        public Task<TReturn> CallAsync<TReturn>(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, from, gas, value);
        }

        public Task<TReturn> CallAsync<TReturn>(string from, HexBigInteger gas,
           HexBigInteger value, BlockParameter block, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, from, gas, value, block);
        }

        public Task<TReturn> CallAsync<TReturn>(BlockParameter block, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(params object[] functionInput)
            where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, from, gas, value);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(string from, HexBigInteger gas,
           HexBigInteger value, BlockParameter block, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, from, gas, value, block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(
             BlockParameter blockParameter, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, blockParameter);
        }

        public Task<HexBigInteger> EstimateGasAsync(params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return EstimateGasFromEncAsync(encodedInput);
        }

        public Task<HexBigInteger> EstimateGasAsync(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return EstimateGasFromEncAsync(encodedInput, from, gas, value);
        }

        public string GetData(params object[] functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature, FunctionABI.InputParameters,
                functionInput);
        }

        public Task<string> SendTransactionAsync(string from, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.SendTransactionAsync(encodedInput, from, null, null);
        }

        public Task<string> SendTransactionAsync(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.SendTransactionAsync(encodedInput, from, gas, value);
        }

        public Task<string> SendTransactionAsync(
            TransactionInput input, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.SendTransactionAsync(encodedInput, input);
        }
    }

    public class Function<TFunctionInput> : FunctionBase
    {
        public Function(Contract contract, FunctionABI functionAbi)
            : base(contract, functionAbi)
        {
        }

        public Task<TReturn> CallAsync<TReturn>()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return base.CallAsync<TReturn>(encodedInput);
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput);
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, from, gas, value);
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput,
             BlockParameter blockParameter)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, blockParameter);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>() where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return base.CallAsync(new TReturn(), encodedInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(BlockParameter block) where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return base.CallAsync(new TReturn(), encodedInput, block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, BlockParameter block) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, block);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, string from,
            HexBigInteger gas,
            HexBigInteger value) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, from, gas, value);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, string from,
           HexBigInteger gas,
           HexBigInteger value, BlockParameter block) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, from, gas, value, block);
        }

        public Task<HexBigInteger> EstimateGasAsync()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return EstimateGasFromEncAsync(encodedInput);
        }

        public Task<HexBigInteger> EstimateGasAsync(TFunctionInput functionInput)
        {
            var encodedInput = GetData(functionInput);
            return EstimateGasFromEncAsync(encodedInput);
        }

        public Task<HexBigInteger> EstimateGasAsync(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return EstimateGasFromEncAsync(encodedInput, from, gas, value);
        }

        public Task<HexBigInteger> EstimateGasAsync(TFunctionInput functionInput,
            CallInput callInput)
        {
            var encodedInput = GetData(functionInput);
            return EstimateGasFromEncAsync(encodedInput, callInput);
        }

        public string GetData(TFunctionInput functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(functionInput, FunctionABI.Sha3Signature);
        }

        public Task<string> SendTransactionAsync(TFunctionInput functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.SendTransactionAsync(encodedInput);
        }

        public Task<string> SendTransactionAsync(TFunctionInput functionInput, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = GetData(functionInput);
            return base.SendTransactionAsync(encodedInput, from, gas, value);
        }

        public Task<string> SendTransactionAsync(TFunctionInput functionInput,
            TransactionInput input)
        {
            var encodedInput = GetData(functionInput);
            return base.SendTransactionAsync(encodedInput, input);
        }
    }
}