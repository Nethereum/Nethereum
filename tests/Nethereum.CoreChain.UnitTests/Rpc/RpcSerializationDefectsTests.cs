using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.CoreChain.Models;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class RpcSerializationDefectsTests
    {
        private static BlockHeader PreLondonHeader() => new BlockHeader
        {
            ParentHash = new byte[32],
            UnclesHash = new byte[32],
            Coinbase = "0x0000000000000000000000000000000000000000",
            StateRoot = new byte[32],
            TransactionsHash = new byte[32],
            ReceiptHash = new byte[32],
            LogsBloom = new byte[256],
            Difficulty = 0x20000,
            BlockNumber = 1,
            GasLimit = 30_000_000,
            GasUsed = 0,
            Timestamp = 10,
            ExtraData = new byte[0],
            MixHash = new byte[32],
            Nonce = new byte[8],
            BaseFee = null
        };

        [Fact]
        public void Given_PreLondonBlock_When_SerializedForTheWire_Then_BaseFeePerGasKeyIsOmitted()
        {
            var block = PreLondonHeader().ToBlockWithTransactionHashes(new byte[32], new string[0], blockSize: 0);

            var json = JsonSerializer.Serialize(block, CoreChainJsonContext.Default.BlockWithTransactionHashes);

            Assert.DoesNotContain("baseFeePerGas", json);
        }

        [Fact]
        public void Given_LondonBlock_When_SerializedForTheWire_Then_BaseFeePerGasKeyIsPresent()
        {
            var header = PreLondonHeader();
            header.BaseFee = new EvmUInt256(1_000_000_000UL);

            var block = header.ToBlockWithTransactionHashes(new byte[32], new string[0], blockSize: 0);
            var json = JsonSerializer.Serialize(block, CoreChainJsonContext.Default.BlockWithTransactionHashes);

            Assert.Contains("baseFeePerGas", json);
        }

        private const string AddressTx0 = "0x1111111111111111111111111111111111111111";
        private const string AddressTx1 = "0x2222222222222222222222222222222222222222";

        private static InMemoryLogStore StoreWithTwoTxBlock()
        {
            var store = new InMemoryLogStore();
            var blockHash = new byte[32];
            blockHash[0] = 0xab;

            var tx0 = new byte[32]; tx0[0] = 0x01;
            var tx1 = new byte[32]; tx1[0] = 0x02;

            store.SaveLogsAsync(
                new List<Log> { Log.Create(new byte[32], AddressTx0), Log.Create(new byte[32], AddressTx0), Log.Create(new byte[32], AddressTx0) },
                tx0, blockHash, blockNumber: 2, txIndex: 0);
            store.SaveLogsAsync(
                new List<Log> { Log.Create(new byte[32], AddressTx1), Log.Create(new byte[32], AddressTx1) },
                tx1, blockHash, blockNumber: 2, txIndex: 1);

            return store;
        }

        [Fact]
        public async Task Given_BlockWithLogsAcrossTwoTxs_When_AllQueried_Then_LogIndexIsBlockGlobalNotPerTx()
        {
            var store = StoreWithTwoTxBlock();

            var logs = await store.GetLogsAsync(new LogFilter { FromBlock = 2, ToBlock = 2 });

            Assert.Equal(5, logs.Count);
            Assert.Equal(new[] { 0, 1, 2, 3, 4 }, logs.ConvertAll(l => l.LogIndex).ToArray());
        }

        [Fact]
        public async Task Given_SecondTxAddressFilter_When_Queried_Then_MatchedLogsKeepTheirBlockGlobalLogIndex()
        {
            var store = StoreWithTwoTxBlock();

            var filter = new LogFilter { FromBlock = 2, ToBlock = 2, Addresses = new List<string> { AddressTx1 } };
            var logs = await store.GetLogsAsync(filter);

            Assert.Equal(2, logs.Count);
            Assert.Equal(new[] { 3, 4 }, logs.ConvertAll(l => l.LogIndex).ToArray());
        }
    }
}
