using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;
using System.Threading;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionReceipts;

namespace Nethereum.RPC.TransactionManagers
{
    public interface ITransactionManager
    {
        IClient Client { get; set; }
        Task<string> SendTransactionAsync(TransactionInput transactionInput);
        Task<HexBigInteger> EstimateGasAsync(CallInput callInput);
        Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount);
        BigInteger DefaultGasPrice { get; set; }
        BigInteger DefaultGas { get; set; }
        IAccount Account { get; }
        Task<string> SignTransactionAsync(TransactionInput transaction);
        Task<string> SignTransactionRetrievingNextNonceAsync(TransactionInput transaction);

#if !DOTNET35
        ITransactionReceiptService TransactionReceiptService { get; set; }
        Task<TransactionReceipt> SendTransactionAndWaitForReceiptAsync(TransactionInput transactionInput, CancellationTokenSource tokenSource);
#endif

    }
}
