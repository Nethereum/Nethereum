using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Rpc.Handlers.Standard;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.CoreChain.Tracing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.RPC.DebugNode.Dtos.Tracing;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;
using NonceChange = Nethereum.Model.NonceChange;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class EthGetBlockAccessListHandlerTests
    {
        private const string Address = "0xa94f5374fce5edbc8e2a8697c15331677e6ebf0b";

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_ARpcClientAsksForItsAccessList_Then_ItReceivesTheRetainedList()
        {
            var chain = await AmsterdamChainRetainingAListAsync();

            var accounts = await AskForAsync(chain, "0x1");

            Assert.Equal(Address, Assert.Single(accounts).Address);
        }

        [Fact]
        public async Task Given_AnAmsterdamBlock_When_ItIsAskedForByHash_Then_TheSameListComesBack()
        {
            var chain = await AmsterdamChainRetainingAListAsync();

            var accounts = await AskForAsync(chain, chain.BlockHash.ToHex(true));

            Assert.Equal(Address, Assert.Single(accounts).Address);
        }

        [Fact]
        public async Task Given_APreAmsterdamBlockThatSomehowHasARetainedList_When_Asked_Then_ItIsResourceNotFound()
        {
            var chain = await ChainAsync(blockAccessListHash: null, retainList: true);

            var response = await AskAsync(chain, "0x1");

            Assert.True(response.HasError);
            Assert.Equal(-32001, response.Error.Code);
        }

        [Fact]
        public async Task Given_AnAmsterdamBlockWhoseListIsGone_When_Asked_Then_ItIsPrunedHistoryUnavailable()
        {
            var chain = await ChainAsync(blockAccessListHash: SomeHash(), retainList: false);

            var response = await AskAsync(chain, "0x1");

            Assert.True(response.HasError);
            Assert.Equal(4444, response.Error.Code);
        }

        [Fact]
        public async Task Given_ABlockTheNodeDoesNotHave_When_Asked_Then_TheResultIsNullAndNotAnError()
        {
            var chain = await AmsterdamChainRetainingAListAsync();

            var response = await AskAsync(chain, "0x9");

            Assert.False(response.HasError);
            Assert.Null(response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_ABlockHashTheNodeDoesNotHave_When_Asked_Then_TheResultIsNullAndNotAnError()
        {
            var chain = await AmsterdamChainRetainingAListAsync();

            var response = await AskAsync(chain, new byte[32].ToHex(true));

            Assert.False(response.HasError);
            Assert.Null(response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_ThePendingTag_When_Asked_Then_TheResultIsNullRatherThanTheHeadsList()
        {
            var chain = await AmsterdamChainRetainingAListAsync();

            var response = await AskAsync(chain, "pending");

            Assert.False(response.HasError);
            Assert.Null(response.ResultNewtonsoft);
        }

        [Fact]
        public async Task Given_ANodeThatRetainsNoAccessListsAtAll_When_AnAmsterdamBlockIsAsked_Then_ItIsNotReportedAsPruned()
        {
            var chain = await ChainAsync(blockAccessListHash: SomeHash(), retainList: false, retainsAccessLists: false);

            var response = await AskAsync(chain, "0x1");

            Assert.True(response.HasError);
            Assert.NotEqual(4444, response.Error.Code);
            Assert.Equal(-32004, response.Error.Code);
        }

        private static async Task<TestChain> AmsterdamChainRetainingAListAsync() =>
            await ChainAsync(blockAccessListHash: SomeHash(), retainList: true);

        private static async Task<TestChain> ChainAsync(
            byte[] blockAccessListHash,
            bool retainList,
            bool retainsAccessLists = true)
        {
            var blocks = new InMemoryBlockStore();
            var accessLists = retainsAccessLists ? new InMemoryBlockAccessListStore(blocks) : null;
            var blockHash = SomeHash();

            await blocks.SaveAsync(
                new BlockHeader { BlockNumber = 1, BlockAccessListHash = blockAccessListHash },
                blockHash);

            if (retainList)
                await accessLists.SaveAsync(blockHash, BlockAccessListRLPEncoder.Current.Encode(OneAccount()));

            return new TestChain(new StoreBackedChainNode(blocks, accessLists), blockHash);
        }

        private static List<AccountChanges> OneAccount()
        {
            var account = new AccountChanges(Address);
            account.NonceChanges.Add(new NonceChange(0, 1));
            return new List<AccountChanges> { account };
        }

        private static byte[] SomeHash()
        {
            var hash = new byte[32];
            hash[31] = 0xaa;
            return hash;
        }

        private static async Task<List<AccountAccessDto>> AskForAsync(TestChain chain, string blockParameter)
        {
            var response = await AskAsync(chain, blockParameter);
            Assert.False(response.HasError, response.Error?.Message);
            return Assert.IsType<List<AccountAccessDto>>(response.ResultNewtonsoft);
        }

        private static Task<RpcResponseMessage> AskAsync(TestChain chain, string blockParameter) =>
            new EthGetBlockAccessListHandler().HandleAsync(
                new RpcRequestMessage(1, "eth_getBlockAccessList", blockParameter),
                new RpcContext(chain.Node, chainId: 1, services: null));

        private sealed class TestChain
        {
            public TestChain(IChainNode node, byte[] blockHash)
            {
                Node = node;
                BlockHash = blockHash;
            }

            public IChainNode Node { get; }
            public byte[] BlockHash { get; }
        }

        private sealed class StoreBackedChainNode : IChainNode
        {
            private readonly IBlockStore _blocks;

            public StoreBackedChainNode(IBlockStore blocks, IBlockAccessListStore blockAccessLists)
            {
                _blocks = blocks;
                BlockAccessLists = blockAccessLists;
            }

            public IBlockStore Blocks => _blocks;
            public IBlockAccessListStore BlockAccessLists { get; }

            public Task<BigInteger> GetBlockNumberAsync() => _blocks.GetHeightAsync();
            public Task<byte[]> GetBlockHashByNumberAsync(BigInteger blockNumber) => _blocks.GetHashByNumberAsync(blockNumber);

            public ChainConfig Config => throw new NotImplementedException();
            public ITransactionStore Transactions => throw new NotImplementedException();
            public IUncleStore Uncles => throw new NotImplementedException();
            public IReceiptStore Receipts => throw new NotImplementedException();
            public ILogStore Logs => throw new NotImplementedException();
            public IStateStore State => throw new NotImplementedException();
            public IFilterStore Filters => throw new NotImplementedException();
            public ITrieNodeStore TrieNodes => throw new NotImplementedException();
            public IBlobStore BlobStore => throw new NotImplementedException();
            public IProofService ProofService => throw new NotImplementedException();

            public Task<BlockHeader> GetBlockByHashAsync(byte[] hash) => throw new NotImplementedException();
            public Task<BlockHeader> GetBlockByNumberAsync(BigInteger number) => throw new NotImplementedException();
            public Task<BlockHeader> GetLatestBlockAsync() => throw new NotImplementedException();
            public Task<ISignedTransaction> GetTransactionByHashAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<Receipt> GetTransactionReceiptAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<ReceiptInfo> GetTransactionReceiptInfoAsync(byte[] txHash) => throw new NotImplementedException();
            public Task<BigInteger> GetBalanceAsync(string address) => throw new NotImplementedException();
            public Task<BigInteger> GetNonceAsync(string address) => throw new NotImplementedException();
            public Task<byte[]> GetCodeAsync(string address) => throw new NotImplementedException();
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot) => throw new NotImplementedException();
            public Task<BigInteger> GetBalanceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<BigInteger> GetNonceAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<byte[]> GetCodeAsync(string address, BigInteger blockNumber) => throw new NotImplementedException();
            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 slot, BigInteger blockNumber) => throw new NotImplementedException();
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
            public Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByNumberAsync(BigInteger blockNumber, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<List<BlockResponseItemDto<OpcodeTraceResult>>> TraceBlockByHashAsync(byte[] blockHash, OpcodeTraceConfig config = null) => throw new NotImplementedException();
            public Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByNumberAsync(BigInteger blockNumber) => throw new NotImplementedException();
            public Task<List<BlockResponseItemDto<CallTraceResult>>> TraceBlockCallTracerByHashAsync(byte[] blockHash) => throw new NotImplementedException();
            public Task<byte[]> CaptureBlockWitnessAsync(long blockNumber) => throw new NotImplementedException();
            public Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<OpcodeTraceResult> TraceCallAsync(CallInput callInput, BigInteger blockNumber, OpcodeTraceConfig config = null, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<CallTraceResult> TraceCallCallTracerAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<PrestateTraceResult> TraceCallPrestateAsync(CallInput callInput, BigInteger blockNumber, Dictionary<string, StateOverride> stateOverrides = null) => throw new NotImplementedException();
            public Task<List<EthSimulateBlockResult>> SimulateAsync(EthSimulateInput input, BigInteger? baseBlockNumber) => throw new NotImplementedException();
        }
    }
}
