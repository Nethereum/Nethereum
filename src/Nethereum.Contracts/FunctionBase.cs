using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.TransactionManagers;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public abstract class FunctionBase
    {
        private readonly Contract _contract;

        protected FunctionBase(Contract contract, FunctionABI functionAbi)
        {
            FunctionABI = functionAbi;
            _contract = contract;
            FunctionCallDecoder = new FunctionCallDecoder();
            FunctionCallEncoder = new FunctionCallEncoder();
        }

        private EthCall EthCall => _contract.Eth.Transactions.Call;
        private EthEstimateGas EthEstimateGas => _contract.Eth.Transactions.EstimateGas;
        protected ITransactionManager TransactionManager => _contract.Eth.TransactionManager;

        public BlockParameter DefaultBlock => _contract.DefaultBlock;

        public string ContractAddress => _contract.Address;

        protected FunctionCallDecoder FunctionCallDecoder { get; set; }

        protected FunctionCallEncoder FunctionCallEncoder { get; set; }

        public FunctionABI FunctionABI { get; protected set; }

        public List<ParameterOutput> DecodeInput(string data)
        {
            return FunctionCallDecoder.DecodeFunctionInput(FunctionABI.Sha3Signature, data, FunctionABI.InputParameters);
        }

        public Task<string> SendTransactionAsync(string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return SendTransactionAsync(encodedInput, from, gas, value);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall)
        {
            var result =
                await
                    EthCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress), DefaultBlock)
                        .ConfigureAwait(false);

            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var result =
                await
                    EthCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, @from, gas, value),
                        DefaultBlock).ConfigureAwait(false);
            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, CallInput callInput)
        {
            callInput.Data = encodedFunctionCall;
            var result = await EthCall.SendRequestAsync(callInput, DefaultBlock).ConfigureAwait(false);
            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, CallInput callInput,
            BlockParameter block)
        {
            callInput.Data = encodedFunctionCall;
            var result = await EthCall.SendRequestAsync(callInput, block).ConfigureAwait(false);
            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall)
        {
            var result =
                await
                    EthCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress), DefaultBlock)
                        .ConfigureAwait(false);

            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall, string from,
            HexBigInteger gas, HexBigInteger value)
        {
            var result =
                await
                    EthCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, @from, gas, value),
                        DefaultBlock).ConfigureAwait(false);
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall,
            CallInput callInput)
        {
            callInput.Data = encodedFunctionCall;
            var result = await EthCall.SendRequestAsync(callInput, DefaultBlock).ConfigureAwait(false);
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall,
            CallInput callInput, BlockParameter block)
        {
            callInput.Data = encodedFunctionCall;
            var result = await EthCall.SendRequestAsync(callInput, block).ConfigureAwait(false);
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(string encodedFunctionCall)
        {
            return
                await
                    EthEstimateGas.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress))
                        .ConfigureAwait(false);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(string encodedFunctionCall, string from,
            HexBigInteger gas, HexBigInteger value)
        {
            return
                await
                    EthEstimateGas.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, @from, gas,
                        value)).ConfigureAwait(false);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(string encodedFunctionCall, CallInput callInput
        )
        {
            callInput.Data = encodedFunctionCall;
            return await EthEstimateGas.SendRequestAsync(callInput).ConfigureAwait(false);
        }

        protected Task<string> SendTransactionAsync(string encodedFunctionCall)
        {
            return SendTransactionAsync(new TransactionInput(encodedFunctionCall, ContractAddress));
        }

        protected Task<string> SendTransactionAsync(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return
                SendTransactionAsync(new TransactionInput(encodedFunctionCall, ContractAddress, from, gas,
                    value));
        }

        protected Task<string> SendTransactionAsync(string encodedFunctionCall,
            TransactionInput input)
        {
            input.Data = encodedFunctionCall;
            return SendTransactionAsync(input);
        }

        protected Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            return TransactionManager.SendTransactionAsync(transactionInput);
        }

        private Parameter GetFirstParameterOrNull(Parameter[] parameters)
        {
            if (parameters == null) return null;
            if (parameters.Length == 0) return null;
            return parameters[0];
        }
    }
}