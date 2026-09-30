using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EngineApiRoundTripTests
    {
        private const string FeeRecipient = "0x0000000000000000000000000000000000009999";

        private static readonly string ZeroHash32 = "0x" + new string('0', 64);

        private static async Task<(DevChainNode Node, IEngineApiService Engine)> CreateNodeAsync(string hardfork = "cancun")
        {
            var config = new DevChainConfig
            {
                Hardfork = hardfork,
                AutoMine = false
            };

            var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync();

            var importer = new BlockImporter(
                node.BlockManager.Engine,
                node.Blocks,
                node.State,
                node.Transactions,
                node.Receipts,
                node.Logs);

            var forkChoice = new DifficultyForkChoice(node.Blocks);
            var registry = new PayloadBuildRegistry();
            var engine = new EngineApiService(
                node.Blocks, importer, node.BlockManager.BlockProducer, forkChoice, registry, node.Config);

            return (node, engine);
        }

        private static string HashHex(byte[] hash) => hash.ToHex(true);

        private static PayloadAttributesV3 NextAttributes(long timestamp) => new PayloadAttributesV3
        {
            Timestamp = new HexBigInteger(timestamp),
            PrevRandao = "0x" + new string('7', 64),
            SuggestedFeeRecipient = FeeRecipient,
            ParentBeaconBlockRoot = ZeroHash32,
            Withdrawals = new List<Nethereum.RPC.Eth.DTOs.Withdrawal>()
        };

        private static ForkchoiceStateV1 StateFor(string hashHex) => new ForkchoiceStateV1
        {
            HeadBlockHash = hashHex,
            SafeBlockHash = hashHex,
            FinalizedBlockHash = hashHex
        };

        private static PayloadAttributesV4 NextAttributesV4(long timestamp) => new PayloadAttributesV4
        {
            Timestamp = new HexBigInteger(timestamp),
            PrevRandao = "0x" + new string('7', 64),
            SuggestedFeeRecipient = FeeRecipient,
            ParentBeaconBlockRoot = ZeroHash32,
            Withdrawals = new List<Nethereum.RPC.Eth.DTOs.Withdrawal>()
        };

        [Fact]
        public async Task Given_AnEmptyMempool_When_ForkchoiceUpdatedWithAttributesThenGetPayloadThenNewPayloadThenForkchoiceUpdatedAgain_Then_TheLoopClosesAsValidThroughout()
        {
            var (node, engine) = await CreateNodeAsync();

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attrs = NextAttributes(head.Timestamp + 1);

            var fcuResult = await engine.ForkchoiceUpdatedAsync(StateFor(HashHex(headHash)), attrs);

            Assert.Equal(EnginePayloadStatus.Valid, fcuResult.PayloadStatus.Status);
            Assert.False(string.IsNullOrEmpty(fcuResult.PayloadId));

            var payload = await engine.GetPayloadAsync(fcuResult.PayloadId);

            Assert.Equal(FeeRecipient, payload.FeeRecipient);
            Assert.Equal(attrs.Timestamp.Value, payload.Timestamp.Value);
            Assert.Equal(HashHex(headHash), payload.ParentHash);
            Assert.Equal(head.BlockNumber.ToBigInteger() + 1, payload.BlockNumber.Value);

            var newPayloadStatus = await engine.NewPayloadAsync(payload, attrs.ParentBeaconBlockRoot);

            Assert.Equal(EnginePayloadStatus.Valid, newPayloadStatus.Status);
            Assert.Equal(payload.BlockHash, newPayloadStatus.LatestValidHash);

            var blockAfterImport = await node.Blocks.GetByHashAsync(payload.BlockHash.HexToByteArray());
            Assert.NotNull(blockAfterImport);

            var fcu2 = await engine.ForkchoiceUpdatedAsync(StateFor(payload.BlockHash), null);

            Assert.Equal(EnginePayloadStatus.Valid, fcu2.PayloadStatus.Status);

            var newHead = await node.Blocks.GetLatestAsync();
            Assert.Equal(head.BlockNumber.ToBigInteger() + 1, newHead.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_APayloadWithAnUnknownParentHash_When_NewPayloadIsCalled_Then_TheResultIsSyncing()
        {
            var (node, engine) = await CreateNodeAsync();

            var payload = new ExecutionPayloadV3
            {
                ParentHash = "0x" + new string('a', 64),
                FeeRecipient = FeeRecipient,
                StateRoot = ZeroHash32,
                ReceiptsRoot = ZeroHash32,
                LogsBloom = "0x" + new string('0', 512),
                PrevRandao = ZeroHash32,
                BlockNumber = new HexBigInteger(1),
                GasLimit = new HexBigInteger(30_000_000),
                GasUsed = new HexBigInteger(0),
                Timestamp = new HexBigInteger(1),
                ExtraData = "0x",
                BaseFeePerGas = new HexBigInteger(1_000_000_000),
                BlockHash = "0x" + new string('b', 64),
                Transactions = new List<string>(),
                Withdrawals = new List<Nethereum.RPC.Eth.DTOs.Withdrawal>(),
                BlobGasUsed = new HexBigInteger(0),
                ExcessBlobGas = new HexBigInteger(0)
            };

            var status = await engine.NewPayloadAsync(payload, ZeroHash32);

            Assert.Equal(EnginePayloadStatus.Syncing, status.Status);
            Assert.Null(status.LatestValidHash);

            var head = await node.Blocks.GetLatestAsync();
            Assert.Equal(BigInteger.Zero, head.BlockNumber.ToBigInteger());
        }

        [Fact]
        public async Task Given_APayloadWithAMismatchedGasUsed_When_NewPayloadIsCalled_Then_TheResultIsInvalidWithAValidationError()
        {
            var (node, engine) = await CreateNodeAsync();

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attrs = NextAttributes(head.Timestamp + 1);

            var fcuResult = await engine.ForkchoiceUpdatedAsync(StateFor(HashHex(headHash)), attrs);
            var payload = await engine.GetPayloadAsync(fcuResult.PayloadId);

            payload.GasUsed = new HexBigInteger(payload.GasUsed.Value + 1);

            var status = await engine.NewPayloadAsync(payload, attrs.ParentBeaconBlockRoot);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.False(string.IsNullOrEmpty(status.ValidationError));
        }

        [Fact]
        public async Task Given_AnExecutionRequestWithEmptyRequestData_When_NewPayloadV4IsCalled_Then_InvalidParamsIsThrown()
        {
            var (node, engine) = await CreateNodeAsync("prague");

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attrs = NextAttributesV4(head.Timestamp + 1);

            var fcuResult = await engine.ForkchoiceUpdatedV4Async(StateFor(HashHex(headHash)), attrs);
            var built = await engine.GetPayloadV4Async(fcuResult.PayloadId);

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                engine.NewPayloadV4Async(built.ExecutionPayload, new[] { "0x00" }, attrs.ParentBeaconBlockRoot));

            Assert.Equal(-32602, ex.Code);
        }

        [Fact]
        public async Task Given_ExecutionRequestsOutOfOrder_When_NewPayloadV4IsCalled_Then_InvalidParamsIsThrown()
        {
            var (node, engine) = await CreateNodeAsync("prague");

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attrs = NextAttributesV4(head.Timestamp + 1);

            var fcuResult = await engine.ForkchoiceUpdatedV4Async(StateFor(HashHex(headHash)), attrs);
            var built = await engine.GetPayloadV4Async(fcuResult.PayloadId);

            var outOfOrder = new[] { "0x0102", "0x0001" };

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                engine.NewPayloadV4Async(built.ExecutionPayload, outOfOrder, attrs.ParentBeaconBlockRoot));

            Assert.Equal(-32602, ex.Code);
        }

        [Fact]
        public async Task Given_ExecutionRequestsWithADuplicateType_When_NewPayloadV4IsCalled_Then_InvalidParamsIsThrown()
        {
            var (node, engine) = await CreateNodeAsync("prague");

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attrs = NextAttributesV4(head.Timestamp + 1);

            var fcuResult = await engine.ForkchoiceUpdatedV4Async(StateFor(HashHex(headHash)), attrs);
            var built = await engine.GetPayloadV4Async(fcuResult.PayloadId);

            var duplicateType = new[] { "0x0001", "0x0002" };

            var ex = await Assert.ThrowsAsync<RpcException>(() =>
                engine.NewPayloadV4Async(built.ExecutionPayload, duplicateType, attrs.ParentBeaconBlockRoot));

            Assert.Equal(-32602, ex.Code);
        }

        [Fact]
        public async Task Given_NoExecutionRequests_When_NewPayloadV4IsCalled_Then_TheResultIsValid()
        {
            var (node, engine) = await CreateNodeAsync("prague");

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attrs = NextAttributesV4(head.Timestamp + 1);

            var fcuResult = await engine.ForkchoiceUpdatedV4Async(StateFor(HashHex(headHash)), attrs);
            var built = await engine.GetPayloadV4Async(fcuResult.PayloadId);

            var status = await engine.NewPayloadV4Async(
                built.ExecutionPayload, System.Array.Empty<string>(), attrs.ParentBeaconBlockRoot);

            Assert.Equal(EnginePayloadStatus.Valid, status.Status);
        }

        private static ExecutionPayloadV3 PayloadWithRealParent(
            string parentHashHex, BigInteger blockNumber, BigInteger timestamp,
            BigInteger blobGasUsed, BigInteger gasLimit) => new ExecutionPayloadV3
        {
            ParentHash = parentHashHex,
            FeeRecipient = FeeRecipient,
            StateRoot = ZeroHash32,
            ReceiptsRoot = ZeroHash32,
            LogsBloom = "0x" + new string('0', 512),
            PrevRandao = ZeroHash32,
            BlockNumber = new HexBigInteger(blockNumber),
            GasLimit = new HexBigInteger(gasLimit),
            GasUsed = new HexBigInteger(0),
            Timestamp = new HexBigInteger(timestamp),
            ExtraData = "0x",
            BaseFeePerGas = new HexBigInteger(1_000_000_000),
            BlockHash = "0x" + new string('b', 64),
            Transactions = new List<string>(),
            Withdrawals = new List<Nethereum.RPC.Eth.DTOs.Withdrawal>(),
            BlobGasUsed = new HexBigInteger(blobGasUsed),
            ExcessBlobGas = new HexBigInteger(0)
        };

        [Fact]
        public async Task Given_APayloadTimestampAtUInt64MinusTwo_When_NewPayloadIsCalled_Then_NoOverflowExceptionIsThrown()
        {
            var (node, engine) = await CreateNodeAsync();

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());

            var payload = PayloadWithRealParent(
                HashHex(headHash),
                head.BlockNumber.ToBigInteger() + 1,
                BigInteger.Parse("18446744073709551614"),
                blobGasUsed: 0,
                gasLimit: 30_000_000);

            var status = await engine.NewPayloadAsync(payload, ZeroHash32);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.DoesNotContain("OverflowException", status.ValidationError);
        }

        [Fact]
        public async Task Given_ABlobGasUsedAtUInt64Max_When_NewPayloadIsCalled_Then_TheResultIsInvalidNotAnException()
        {
            var (node, engine) = await CreateNodeAsync();

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());

            var payload = PayloadWithRealParent(
                HashHex(headHash),
                head.BlockNumber.ToBigInteger() + 1,
                head.Timestamp + 1,
                blobGasUsed: BigInteger.Parse("18446744073709551615"),
                gasLimit: 30_000_000);

            var status = await engine.NewPayloadAsync(payload, ZeroHash32);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.DoesNotContain("OverflowException", status.ValidationError);
        }

        [Fact]
        public async Task Given_AGasLimitAboveUInt64Max_When_NewPayloadIsCalled_Then_InvalidParamsIsThrown()
        {
            var (node, engine) = await CreateNodeAsync();

            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());

            var payload = PayloadWithRealParent(
                HashHex(headHash),
                head.BlockNumber.ToBigInteger() + 1,
                head.Timestamp + 1,
                blobGasUsed: 0,
                gasLimit: BigInteger.Pow(2, 64));

            var ex = await Assert.ThrowsAsync<RpcException>(() => engine.NewPayloadAsync(payload, ZeroHash32));

            Assert.Equal(-32602, ex.Code);
        }
    }
}
