using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Tracing;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.EVM.BlockchainState;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class EthGetStorageAtHandlerTrimmedSlotTests
    {
        private const string Address = "0x1234567890123456789012345678901234567890";

        private sealed class MinimalChainNode : IChainNode
        {
            private readonly IStateReader _nodeDataService;
            public MinimalChainNode(IStateReader nodeDataService) => _nodeDataService = nodeDataService;

            public ChainConfig Config => throw new NotImplementedException();
            public IBlockStore Blocks => throw new NotImplementedException();
            public ITransactionStore Transactions => throw new NotImplementedException();
            public IUncleStore Uncles => throw new NotImplementedException();
            public IReceiptStore Receipts => throw new NotImplementedException();
            public ILogStore Logs => throw new NotImplementedException();
            public IStateStore State => throw new NotImplementedException();
            public IFilterStore Filters => throw new NotImplementedException();
            public ITrieNodeStore TrieNodes => throw new NotImplementedException();
            public IBlobStore BlobStore => throw new NotImplementedException();
            public IBlockAccessListStore BlockAccessLists => throw new NotImplementedException();
            public IProofService ProofService => throw new NotImplementedException();

            public Task<BigInteger> GetBlockNumberAsync() => Task.FromResult(BigInteger.One);
            public Task<BlockHeader> GetBlockByHashAsync(byte[] hash) => throw new NotImplementedException();
            public Task<BlockHeader> GetBlockByNumberAsync(BigInteger number) => throw new NotImplementedException();
            public Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();
            public Task<BlockHeader> GetLatestBlockAsync() => throw new NotImplementedException();
            public Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<Receipt> GetTransactionReceiptAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<BigInteger> GetBalanceAsync(string address) => throw new NotImplementedException();
            public Task<BigInteger> GetNonceAsync(string address) => throw new NotImplementedException();
            public Task<byte[]> GetCodeAsync(string address) => throw new NotImplementedException();
            public Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 slot) => throw new NotImplementedException();
            public Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();

            public Task<byte[]> GetStorageAtAsync(string address, Nethereum.Util.EvmUInt256 slot, BigInteger blockNumber)
                => _nodeDataService.GetStorageAtAsync(address, slot);

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
            public Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByNumberAsync(BigInteger blockNumber, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByHashAsync(byte[] blockHash, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();
            public Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByHashAsync(byte[] blockHash) => throw new NotImplementedException();
            public Task<List<EthSimulateBlockResult>> SimulateAsync(EthSimulateInput input, BigInteger? baseBlockNumber) => throw new NotImplementedException();
        }

        [Fact]
        public async Task EthGetStorageAt_TrimmedLeadingZeroSlot_ReturnsFull32ByteHex()
        {
            var flatStore = new InMemoryStateStore();
            var paddedInput = new byte[32];
            paddedInput[30] = 0x01;
            paddedInput[31] = 0x02;
            await flatStore.SaveStorageAsync(Address, BigInteger.One, paddedInput);

            var durable = await flatStore.GetStorageAsync(Address, BigInteger.One);
            Assert.Equal(2, durable.Length);

            var nodeDataService = new StateStoreNodeDataService(flatStore);
            var node = new MinimalChainNode(nodeDataService);
            var context = new RpcContext(node, chainId: 1, services: null);
            var handler = new EthGetStorageAtHandler();

            var request = new RpcRequestMessage(1, "eth_getStorageAt", Address, "0x1", "latest");
            var response = await handler.HandleAsync(request, context);

            Assert.Equal(
                "0x0000000000000000000000000000000000000000000000000000000000000102",
                response.ResultNewtonsoft);
        }
    }
}
