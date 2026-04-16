using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Tracing;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;

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
        ITrieNodeStore TrieNodes { get; }

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
        Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 slot);

        Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber);
        Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber);
        Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber);
        Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 slot, BigInteger blockNumber);

        Task<CallResult> CallAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<CallResult> CallAsync(string to, byte[] data, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null);
        Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx);

        Task<List<ISignedTransaction>> GetPendingTransactionsAsync();

        Task<OpcodeTraceResult> TraceTransactionAsync(string txHash, OpcodeTraceConfig config = null);
        Task<CallTraceResult> TraceTransactionCallTracerAsync(string txHash);
        Task<PrestateTraceResult> TraceTransactionPrestateAsync(string txHash);
        Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null);
        Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, BigInteger blockNumber, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null);
        Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null);
        Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null);
        Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null);
        Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null);
    }
}
