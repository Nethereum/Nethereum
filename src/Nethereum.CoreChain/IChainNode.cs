using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;

namespace Nethereum.CoreChain
{
    public interface IChainNode
    {
        ChainConfig Config { get; }
        IBlockStore Blocks { get; }
        ITransactionStore Transactions { get; }
        IReceiptStore Receipts { get; }
        ILogStore Logs { get; }
        IStateStore State { get; }
        IFilterStore Filters { get; }

        Task<BigInteger> GetBlockNumberAsync();
        Task<BlockHeader> GetBlockByHashAsync(byte[] hash);
        Task<BlockHeader> GetBlockByNumberAsync(BigInteger number);
        Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber);
        Task<BlockHeader> GetLatestBlockAsync();

        Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash);
        Task<Receipt> GetTransactionReceiptAsync(byte[] txHash);
        Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash);

        Task<BigInteger> GetBalanceAsync(string address);
        Task<BigInteger> GetNonceAsync(string address);
        Task<byte[]> GetCodeAsync(string address);
        Task<byte[]> GetStorageAtAsync(string address, BigInteger slot);

        Task<CallResult> CallAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx);

        List<ISignedTransaction> GetPendingTransactions();
    }
}
