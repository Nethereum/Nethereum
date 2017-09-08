using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.TransactionManagers;
using System.Threading;

namespace Nethereum.Contracts
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

        public TReturn DecodeSimpleTypeOutput<TReturn>(string output)
        {
           return FunctionCallDecoder.DecodeSimpleTypeOutput<TReturn>(
                   GetFirstParameterOrNull(FunctionABI.OutputParameters), output);
        }

        public TReturn DecodeDTOTypeOutput<TReturn>(string output) where TReturn: new()
        {
            return FunctionCallDecoder.DecodeFunctionOutput<TReturn>(output);
        }

        public Task<string> SendTransactionAsync(string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return SendTransactionAsync(CreateTransactionInput(from, gas, value));
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas,
            HexBigInteger value)
        {
            var encodedInput = FunctionCallEncoder.EncodeRequest(FunctionABI.Sha3Signature);
            return new TransactionInput(encodedInput, from, gas, value);
        }

#if !DOTNET35
        protected async Task<TReturn> CallAsync<TReturn>(CallInput callInput)
        {
            var result =
                await
                    EthCall.SendRequestAsync(callInput, DefaultBlock)
                        .ConfigureAwait(false);

            
            return DecodeSimpleTypeOutput<TReturn>(result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(CallInput callInput, BlockParameter block)
        {
            var result =
                await
                    EthCall.SendRequestAsync(callInput, block)
                        .ConfigureAwait(false);

            return DecodeSimpleTypeOutput<TReturn>(result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, CallInput callInput)
        {
            var result =
                await
                    EthCall.SendRequestAsync(callInput, DefaultBlock)
                        .ConfigureAwait(false);

            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, CallInput callInput, BlockParameter block)
        {
            var result =
                await
                    EthCall.SendRequestAsync(callInput, block)
                        .ConfigureAwait(false);

            return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result);
        }

        protected async Task<HexBigInteger> EstimateGasFromEncAsync(CallInput callInput)
        {
            return
                await
                    TransactionManager.EstimateGasAsync(callInput)
                        .ConfigureAwait(false);
        }
#else
        protected Task<TReturn> CallAsync<TReturn>(CallInput callInput)
        {

           return EthCall.SendRequestAsync(callInput, DefaultBlock).ContinueWith(result =>
           {
               if (result.Exception != null) throw result.Exception;
               return DecodeSimpleTypeOutput<TReturn>(result.Result);
           });
        }

        protected Task<TReturn> CallAsync<TReturn>(CallInput callInput, BlockParameter block)
        {
            return EthCall.SendRequestAsync(callInput, block).ContinueWith(result =>
            {
                if (result.Exception != null) throw result.Exception;
                return DecodeSimpleTypeOutput<TReturn>(result.Result);
            });
           
        }

        protected  Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, CallInput callInput)
        {
            return EthCall.SendRequestAsync(callInput, DefaultBlock).ContinueWith(result =>
            {
                if (result.Exception != null) throw result.Exception;
                return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result.Result);
            });
        }

        protected  Task<TReturn> CallAsync<TReturn>(TReturn functionOuput, CallInput callInput, BlockParameter block)
        {
            return EthCall.SendRequestAsync(callInput, block).ContinueWith(result =>
            {
                if (result.Exception != null) throw result.Exception;
                return FunctionCallDecoder.DecodeFunctionOutput(functionOuput, result.Result);
            });
        }

        protected Task<HexBigInteger> EstimateGasFromEncAsync(CallInput callInput)
        {
            return TransactionManager.EstimateGasAsync(callInput);          
        }
#endif

        protected CallInput CreateCallInput(string encodedFunctionCall)
        {
            return new CallInput(encodedFunctionCall, ContractAddress);
        }

        protected CallInput CreateCallInput(string encodedFunctionCall, string @from, HexBigInteger gas, HexBigInteger value)
        {
            return new CallInput(encodedFunctionCall, ContractAddress, @from, gas, value);
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall, string from)
        {
            var tx = new TransactionInput(encodedFunctionCall, ContractAddress) {From = @from};
            return tx;
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall, string from, HexBigInteger gas,
            HexBigInteger value)
        {
            return new TransactionInput(encodedFunctionCall, ContractAddress, from, gas, value);
        }

        protected TransactionInput CreateTransactionInput(string encodedFunctionCall, string from, HexBigInteger gas, HexBigInteger gasPrice,
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

        protected Task<string> SendTransactionAsync(TransactionInput transactionInput)
        {
            return TransactionManager.SendTransactionAsync(transactionInput);
        }

#if !DOTNET35
        protected Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(TransactionInput transactionInput, CancellationTokenSource receiptRequestCancellationToken = null)
        {
            return TransactionManager.TransactionReceiptService.SendRequestAsync(transactionInput, receiptRequestCancellationToken);
        }
#endif

        private Parameter GetFirstParameterOrNull(Parameter[] parameters)
        {
            if (parameters == null) return null;
            if (parameters.Length == 0) return null;
            return parameters[0];
        }
    }
}