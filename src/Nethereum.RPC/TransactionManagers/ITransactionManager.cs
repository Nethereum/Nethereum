using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using System.Numerics;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionReceipts;

namespace Nethereum.RPC.TransactionManagers
{
    public interface ITransactionManager
    {
        IClient Client { get; set; }
        Task<string> SendTransactionAsync<T>(T transactionInput) where T : TransactionInput;
        Task<HexBigInteger> EstimateGasAsync<T>(T callInput) where T : CallInput;
        Task<string> SendTransactionAsync(string from, string to, HexBigInteger amount);
        BigInteger DefaultGasPrice { get; set; }
        BigInteger DefaultGas { get; set; }
        IAccount Account { get; }
#if !DOTNET35
        ITransactionReceiptService TransactionReceiptService { get; set; }
#endif
    }
}
