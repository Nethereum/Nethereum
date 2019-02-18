using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts
{
    public class Function : FunctionBase
    {
        public Function(Contract contract, FunctionBuilder functionBuilder) : base(contract, functionBuilder)
        {
        }

        protected FunctionBuilder FunctionBuilder => (FunctionBuilder) FunctionBuilderBase;

        public CallInput CreateCallInput(params object[] functionInput)
        {
            return FunctionBuilder.CreateCallInput(functionInput);
        }

        public CallInput CreateCallInput(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            return FunctionBuilder.CreateCallInput(from, gas, value, functionInput);
        }

        public string GetData(params object[] functionInput)
        {
            return FunctionBuilder.GetData(functionInput);
        }

        public TransactionInput CreateTransactionInput(string from, params object[] functionInput)
        {
            return FunctionBuilder.CreateTransactionInput(from, functionInput);
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas,
            HexBigInteger value, params object[] functionInput)
        {
            return FunctionBuilder.CreateTransactionInput(from, gas, value, functionInput);
        }

        public TransactionInput CreateTransactionInput(string from, HexBigInteger gas, HexBigInteger gasPrice,
            HexBigInteger value, params object[] functionInput)
        {
            return FunctionBuilder.CreateTransactionInput(from, gas, gasPrice, value, functionInput);
        }

        public TransactionInput CreateTransactionInput(TransactionInput input, params object[] functionInput)
        {
            return FunctionBuilder.CreateTransactionInput(input, functionInput);
        }

#if !DOTNET35

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
            return EstimateGasFromEncAsync(CreateCallInput(from, gas, value, functionInput));
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

        public Task<string> SendTransactionAsync(string from, HexBigInteger gas, HexBigInteger gasPrice,
            HexBigInteger value, params object[] functionInput)
        {
            return base.SendTransactionAsync(CreateTransactionInput(from, gas, gasPrice, value, functionInput));
        }

        public Task<string> SendTransactionAsync(TransactionInput input, params object[] functionInput)
        {
            return base.SendTransactionAsync(CreateTransactionInput(input, functionInput));
        }


        public Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(string from,
            CancellationToken token = default(CancellationToken), params object[] functionInput)
        {
            return base.SendTransactionAndWaitForReceiptAsync(CreateTransactionInput(from, functionInput),
                token);
        }

        public Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(string from, HexBigInteger gas,
            HexBigInteger value, CancellationToken token = default(CancellationToken),
            params object[] functionInput)
        {
            return base.SendTransactionAndWaitForReceiptAsync(CreateTransactionInput(from, gas, value, functionInput),
                token);
        }

        public Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(string from, HexBigInteger gas,
            HexBigInteger gasPrice,
            HexBigInteger value, CancellationToken token = default(CancellationToken),
            params object[] functionInput)
        {
            return base.SendTransactionAndWaitForReceiptAsync(
                CreateTransactionInput(from, gas, gasPrice, value, functionInput), token);
        }

        public Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(TransactionInput input,
            CancellationToken token = default(CancellationToken), params object[] functionInput)
        {
            return base.SendTransactionAndWaitForReceiptAsync(CreateTransactionInput(input, functionInput),
                token);
        }
#endif
    }
}