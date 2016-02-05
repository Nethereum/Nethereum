using System.Threading.Tasks;
using edjCase.JsonRpc.Client;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Generic;

namespace Nethereum.Web3
{


    public class Function : FunctionBase
    {
       

        public Function(RpcClient rpcClient, Contract contract, FunctionABI function) : base(rpcClient, contract, function)
        {
           
        }

        public async Task<TReturn> CallAsync<TReturn>(params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return await base.CallAsync<TReturn>(encodedInput);
        }

        public async Task<TReturn> CallAsync<TReturn>(string from,
            HexBigInteger value, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return await base.CallAsync<TReturn>(encodedInput, from, value);
        }

        public async Task<TReturn> CallAsync<TReturn>(
            EthCallTransactionInput callInput, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return await base.CallAsync<TReturn>(encodedInput, callInput);
        }

        public async Task<TReturn> CallAsync<TReturn>(
            EthCallTransactionInput callInput, BlockParameter blockParameter, params object[] functionInput) where TReturn : new()
        {
            var encodedInput = GetData(functionInput);
            return await base.CallAsync<TReturn>(encodedInput, callInput, blockParameter);
        }

        public async Task<string> SendTransactionAsync(params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return await base.SendTransactionAsync(encodedInput);
        }

        public async Task<string> SendTransactionAsync(string from,
            HexBigInteger value, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return await base.SendTransactionAsync(encodedInput, from, value);
        }

        public async Task<string> SendTransactionAsync(
            EthSendTransactionInput input, params object[] functionInput)
        {
            var encodedInput = GetData(functionInput);
            return await base.SendTransactionAsync(encodedInput, input);
        }

        public string GetData(params object[] functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature, FunctionABI.InputParameters, functionInput);
        }
    }
    
    public class Function<TFunctionInput> : FunctionBase
    {

        public Function(RpcClient rpcClient, Contract contract, FunctionABI functionABI) : base(rpcClient, contract, functionABI)
        {
        }

        public async Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput) where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.CallAsync<TReturn>(encodedInput);
        }

        public async Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput, string from,
            HexBigInteger value) where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.CallAsync<TReturn>(encodedInput, from, value);
        }

        public async Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput,
            EthCallTransactionInput callInput) where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.CallAsync<TReturn>(encodedInput, callInput);
        }

        public async Task<TReturn> CallAsync<TReturn>(TFunctionInput functionInput,
            EthCallTransactionInput callInput, BlockParameter blockParameter) where TReturn : new()
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.CallAsync<TReturn>(encodedInput, callInput, blockParameter);
        }


        public async Task<string> SendTransactionAsync(TFunctionInput functionInput) 
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.SendTransactionAsync(encodedInput);
        }

        public async Task<string> SendTransactionAsync(TFunctionInput functionInput, string from,
            HexBigInteger value) 
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.SendTransactionAsync(encodedInput, from, value);
        }

        public async Task<string> SendTransactionAsync(TFunctionInput functionInput,
            EthSendTransactionInput transactionInput) 
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(functionInput);
            return await base.SendTransactionAsync(encodedInput, transactionInput);
        }

        public string GetData(TFunctionInput functionInput)
        {
            return FunctionCallEncoder.EncodeRequest(functionInput);
        }

    }
}