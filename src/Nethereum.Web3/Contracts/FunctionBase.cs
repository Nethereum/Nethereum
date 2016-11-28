using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Personal;

namespace Nethereum.Web3
{
    public abstract class FunctionBase
    {
        private readonly Contract contract;

        private readonly EthCall ethCall;
        private readonly EthEstimateGas ethEstimateGas;
        private readonly EthSendTransaction ethSendTransaction;
        private readonly PersonalSignAndSendTransaction personalSignAndSendTransaction;
        private IClient rpcClient;

        protected FunctionBase(IClient rpcClient, Contract contract, FunctionABI functionABI)
        {
            FunctionABI = functionABI;
            this.rpcClient = rpcClient;
            this.contract = contract;
            ethCall = new EthCall(rpcClient);
            ethSendTransaction = new EthSendTransaction(rpcClient);
            ethEstimateGas = new EthEstimateGas(rpcClient);
            personalSignAndSendTransaction = new PersonalSignAndSendTransaction(rpcClient);
            FunctionCallDecoder = new FunctionCallDecoder();
            FunctionCallEncoder = new FunctionCallEncoder();
        }

        public BlockParameter DefaultBlock => contract.DefaultBlock;

        public string ContractAddress => contract.Address;

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

        public Task<string> SignAndSendTransactionAsync(string password, HexBigInteger gas, HexBigInteger value,
            string from
        )
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return
                personalSignAndSendTransaction.SendRequestAsync(
                    new TransactionInput(encodedInput, ContractAddress, from, gas,
                        value), password);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall)
        {
            var result =
                await
                    ethCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress), DefaultBlock)
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
                    ethCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, @from, gas, value),
                        DefaultBlock).ConfigureAwait(false);
            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, CallInput callInput)
        {
            callInput.Data = encodedFunctionCall;
            var result = await ethCall.SendRequestAsync(callInput, DefaultBlock).ConfigureAwait(false);
            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(string encodedFunctionCall, CallInput callInput,
            BlockParameter block)
        {
            callInput.Data = encodedFunctionCall;
            var result = await ethCall.SendRequestAsync(callInput, block).ConfigureAwait(false);
            return
                FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                    GetFirstParameterOrNull(FunctionABI.OutputParameters), result);
        }


        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall)
        {
            var result =
                await
                    ethCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress), DefaultBlock)
                        .ConfigureAwait(false);

            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall, string from,
            HexBigInteger gas, HexBigInteger value)
        {
            var result =
                await
                    ethCall.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, @from, gas, value),
                        DefaultBlock).ConfigureAwait(false);
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall,
            CallInput callInput)
        {
            callInput.Data = encodedFunctionCall;
            var result = await ethCall.SendRequestAsync(callInput, DefaultBlock).ConfigureAwait(false);
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, string encodedFunctionCall,
            CallInput callInput, BlockParameter block)
        {
            callInput.Data = encodedFunctionCall;
            var result = await ethCall.SendRequestAsync(callInput, block).ConfigureAwait(false);
            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(string encodedFunctionCall)
        {
            return
                await
                    ethEstimateGas.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress))
                        .ConfigureAwait(false);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(string encodedFunctionCall, string from,
            HexBigInteger gas, HexBigInteger value)
        {
            return
                await
                    ethEstimateGas.SendRequestAsync(new CallInput(encodedFunctionCall, ContractAddress, @from, gas,
                        value)).ConfigureAwait(false);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(string encodedFunctionCall, CallInput callInput
        )
        {
            callInput.Data = encodedFunctionCall;
            return await ethEstimateGas.SendRequestAsync(callInput).ConfigureAwait(false);
        }

        protected Task<string> SendTransactionAsync(string encodedFunctionCall)
        {
            return ethSendTransaction.SendRequestAsync(new TransactionInput(encodedFunctionCall, ContractAddress));
        }

        protected Task<string> SendTransactionAsync(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return
                ethSendTransaction.SendRequestAsync(new TransactionInput(encodedFunctionCall, ContractAddress, from, gas,
                    value));
        }

        protected Task<string> SendTransactionAsync(string encodedFunctionCall,
            TransactionInput input)
        {
            input.Data = encodedFunctionCall;
            return ethSendTransaction.SendRequestAsync(input);
        }

        protected Task<string> SignAndSendTransactionAsync(string password, string encodedFunctionCall)
        {
            return
                personalSignAndSendTransaction.SendRequestAsync(
                    new TransactionInput(encodedFunctionCall, ContractAddress), password);
        }

        protected Task<string> SignAndSendTransactionAsync(string password, string encodedFunctionCall, string from,
            HexBigInteger gas,
            HexBigInteger value)
        {
            return
                personalSignAndSendTransaction.SendRequestAsync(
                    new TransactionInput(encodedFunctionCall, ContractAddress, from, gas,
                        value), password);
        }

        protected Task<string> SignAndSendTransactionAsync(string password, string encodedFunctionCall,
            TransactionInput input)
        {
            input.Data = encodedFunctionCall;
            return personalSignAndSendTransaction.SendRequestAsync(input, password);
        }

        private Parameter GetFirstParameterOrNull(Parameter[] parameters)
        {
            if (parameters == null) return null;
            if (parameters.Length == 0) return null;
            return parameters[0];
        }
    }
}