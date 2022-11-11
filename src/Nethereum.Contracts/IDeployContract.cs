using System.Threading;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts
{
    public interface IDeployContract
    {
        ITransactionManager TransactionManager { get; set; }
        string GetData(string contractByteCode, string abi, params object[] values);
        string GetData<TConstructorParams>(string contractByteCode, TConstructorParams inputParams);

#if !DOTNET35
        Task<HexBigInteger> EstimateGasAsync(string abi, string contractByteCode, string from, params object[] values);
        Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger value, TConstructorParams inputParams);
        Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, TConstructorParams inputParams);
        Task<HexBigInteger> EstimateGasAsync<TConstructorParams>(string contractByteCode, string from, TConstructorParams inputParams);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, HexBigInteger gas,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger value,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from,CancellationToken receiptRequestCancellationToken, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, HexBigInteger gas,CancellationToken receiptRequestCancellationToken, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger value,CancellationToken receiptRequestCancellationToken, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value,CancellationToken receiptRequestCancellationToken, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, TConstructorParams inputParams,CancellationToken receiptRequestCancellationToken);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, TConstructorParams inputParams,CancellationToken receiptRequestCancellationToken);

        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, HexBigInteger gas, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger value, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, CancellationTokenSource receiptRequestCancellationToken = null, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, HexBigInteger gas, CancellationTokenSource receiptRequestCancellationToken = null, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger value, CancellationTokenSource receiptRequestCancellationToken = null, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, CancellationTokenSource receiptRequestCancellationToken = null, params object[] values);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, TConstructorParams inputParams, CancellationTokenSource receiptRequestCancellationToken = null);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from, TConstructorParams inputParams, CancellationTokenSource receiptRequestCancellationToken = null);


        Task<string> SendRequestAsync(string contractByteCode, string from);
        Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas);
        Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger value);
        Task<string> SendRequestAsync(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value);
        Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, HexBigInteger nonce, params object[] values);
        Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, params object[] values);
        Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas, HexBigInteger value, params object[] values);
        Task<string> SendRequestAsync(string abi, string contractByteCode, string from, HexBigInteger gas, params object[] values);
        Task<string> SendRequestAsync(string abi, string contractByteCode, string from, params object[] values);
        Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams);
        Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, HexBigInteger gasPrice, HexBigInteger value, TConstructorParams inputParams);
        Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from, HexBigInteger gas, TConstructorParams inputParams);
        Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from, TConstructorParams inputParams);
        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams,CancellationToken receiptRequestCancellationToken = default);

        Task<TransactionReceipt> SendRequestAndWaitForReceiptAsync<TConstructorParams>(HexBigInteger type, string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams,CancellationToken receiptRequestCancellationToken = default);

        Task<string> SendRequestAsync<TConstructorParams>(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams);

        Task<string> SendRequestAsync<TConstructorParams>(HexBigInteger type, string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce, TConstructorParams inputParams);

        Task<string> SendRequestAsync(string abi, string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce, params object[] values);

        Task<string> SendRequestAsync(HexBigInteger type, string abi, string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce, params object[] values);

        Task<string> SendRequestAsync(string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce);

        Task<string> SendRequestAsync(HexBigInteger type, string contractByteCode, string from,
            HexBigInteger gas, HexBigInteger maxFeePerGas, HexBigInteger maxPriorityFeePerGas, HexBigInteger value, HexBigInteger nonce);
#endif


    }
}