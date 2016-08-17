using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3
{
    public class Function : FunctionBase
    {
        public Function(IClient rpcClient, Contract contract, FunctionABI function)
            : base(rpcClient, contract, function)
        {
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

        public Task<HexBigInteger> EstimateGasAsync(
            CallInput callInput, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return EstimateGasFromEncAsync(encodedInput, callInput);
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

        public Task<TReturn> CallAsync<TReturn>(
            CallInput callInput, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, callInput);
        }

        public Task<TReturn> CallAsync<TReturn>(
            CallInput callInput, BlockParameter blockParameter, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, callInput, blockParameter);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(params object[] functionInput)
            where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(string from, HexBigInteger gas,
            HexBigInteger value, TReturn functionOutput, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, from, gas, value);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(
            CallInput callInput, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, callInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(
            CallInput callInput, BlockParameter blockParameter, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, callInput, blockParameter);
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

        public string GetData(params object[] functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature, FunctionABI.InputParameters,
                functionInput);
        }
    }

    public class Function<TFunctionInput> : FunctionBase
    {
        public Function(IClient rpcClient, Contract contract, FunctionABI functionABI)
            : base(rpcClient, contract, functionABI)
        {
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
            CallInput callInput)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, callInput);
        }

        public Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput,
            CallInput callInput, BlockParameter blockParameter)
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync<TReturn>(encodedInput, callInput, blockParameter);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>() where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return base.CallAsync(new TReturn(), encodedInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput, string from,
            HexBigInteger gas,
            HexBigInteger value) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, from, gas, value);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput,
            CallInput callInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, callInput);
        }

        public Task<TReturn> CallDeserializingToObjectAsync<TReturn>(TFunctionInput functionInput,
            CallInput callInput, BlockParameter blockParameter) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return base.CallAsync(new TReturn(), encodedInput, callInput, blockParameter);
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

        public string GetData(TFunctionInput functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(functionInput, FunctionABI.Sha3Signature);
        }
    }
}