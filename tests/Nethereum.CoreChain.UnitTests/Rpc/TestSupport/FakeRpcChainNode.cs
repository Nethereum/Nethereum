using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Tracing;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.DebugNode.Dtos.Tracing;

namespace Nethereum.CoreChain.UnitTests.Rpc.TestSupport
{
    internal sealed class FakeRpcChainNode : IChainNode
    {
        public ChainConfig Config { get; set; } = new ChainConfig();
        public BigInteger Latest { get; set; }

        public InMemoryLogStore LogStore { get; } = new InMemoryLogStore();
        public InMemoryFilterStore FilterStore { get; } = new InMemoryFilterStore();

        private IBlockStore _blocks;
        private ITransactionStore _transactions;
        private IUncleStore _uncles;

        public IBlockStore Blocks { get => _blocks ?? throw new NotImplementedException(); set => _blocks = value; }
        public ITransactionStore Transactions { get => _transactions ?? throw new NotImplementedException(); set => _transactions = value; }
        public IUncleStore Uncles { get => _uncles ?? throw new NotImplementedException(); set => _uncles = value; }
        public IReceiptStore Receipts => throw new NotImplementedException();
        public ILogStore Logs => LogStore;
        public IStateStore State => throw new NotImplementedException();
        public IFilterStore Filters => FilterStore;
        public ITrieNodeStore TrieNodes => throw new NotImplementedException();
        public IBlobStore BlobStore => throw new NotImplementedException();
        public IBlockAccessListStore BlockAccessLists => throw new NotImplementedException();
        public IProofService ProofService => throw new NotImplementedException();

        public Task<BigInteger> GetBlockNumberAsync() => Task.FromResult(Latest);
        public Task<BlockHeader> GetBlockByHashAsync(byte[] hash) =>
            _blocks != null ? _blocks.GetByHashAsync(hash) : throw new NotImplementedException();
        public Task<BlockHeader> GetBlockByNumberAsync(BigInteger number) =>
            _blocks != null ? _blocks.GetByNumberAsync(number) : Task.FromResult(new BlockHeader { BlockNumber = (ulong)number, Timestamp = 0 });
        public Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber) =>
            _blocks != null ? _blocks.GetHashByNumberAsync(blockNumber) : throw new NotImplementedException();
        public Task<BlockHeader> GetLatestBlockAsync() => throw new NotImplementedException();

        public Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash) => throw new NotImplementedException();
        public Task<Receipt> GetTransactionReceiptAsync(byte[] txHash) => throw new NotImplementedException();
        public Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash) => throw new NotImplementedException();

        public Task<BigInteger> GetBalanceAsync(string address) => throw new NotImplementedException();
        public Task<BigInteger> GetNonceAsync(string address) => throw new NotImplementedException();
        public Task<byte[]> GetCodeAsync(string address) => throw new NotImplementedException();
        public Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 slot) => throw new NotImplementedException();

        public Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber) => Task.FromResult(blockNumber);
        public Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
        public Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
        public Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 slot, BigInteger blockNumber) => throw new NotImplementedException();

        public Task<CallResult> CallAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null, Dictionary<string, StateOverride> stateOverrides = null, List<Authorisation7702Signed> authorisationList = null) => throw new NotImplementedException();
        public Task<CallResult> CallAsync(string to, byte[] data, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null, Dictionary<string, StateOverride> stateOverrides = null, List<Authorisation7702Signed> authorisationList = null) => throw new NotImplementedException();
        public Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
        public Task<CallResult> EstimateContractCreationGasAsync(byte[] initCode, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
        public Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
        public Task<AccessListResult> CreateAccessListAsync(string to, byte[] data, BigInteger blockNumber, string from = null, BigInteger? value = null, BigInteger? gasLimit = null) => throw new NotImplementedException();
        public Task<TransactionExecutionResult> SendTransactionAsync(ISignedTransaction tx) => throw new NotImplementedException();

        public Task<List<ISignedTransaction>> GetPendingTransactionsAsync() => throw new NotImplementedException();

        public Task<List<BlobSidecarRecord>> GetBlobSidecarsByBlockNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();

        public Task<OpcodeTraceResult> TraceTransactionAsync(string txHash, OpcodeTraceConfig config = null) => throw new NotImplementedException();
        public Task<CallTraceResult> TraceTransactionCallTracerAsync(string txHash) => throw new NotImplementedException();
        public Task<PrestateTraceResult> TraceTransactionPrestateAsync(string txHash) => throw new NotImplementedException();
        public Task<byte[]> CaptureBlockWitnessAsync(long blockNumber) => throw new NotImplementedException();
        public Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
        public Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, BigInteger blockNumber, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
        public Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
        public Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
        public Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
        public Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
        public Task<List<EthSimulateBlockResult>> SimulateAsync(EthSimulateInput input, BigInteger? baseBlockNumber) => throw new NotImplementedException();
        public Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByNumberAsync(BigInteger blockNumber, OpcodeTraceConfig config = null) => throw new NotImplementedException();
        public Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByHashAsync(byte[] blockHash, OpcodeTraceConfig config = null) => throw new NotImplementedException();
        public Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();
        public Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByHashAsync(byte[] blockHash) => throw new NotImplementedException();
    }

    internal sealed class SingleServiceProvider : IServiceProvider
    {
        private readonly object _service;
        public SingleServiceProvider(object service) => _service = service;
        public object GetService(Type serviceType) => serviceType.IsInstanceOfType(_service) ? _service : null;
    }

    internal sealed class StubFinalityCursorProvider : Nethereum.CoreChain.Rpc.IFinalityCursorProvider
    {
        public BigInteger? Finalized { get; set; }
        public BigInteger? Safe { get; set; }
        public BigInteger? GetFinalizedBlockNumber() => Finalized;
        public BigInteger? GetSafeBlockNumber() => Safe;
    }
}
