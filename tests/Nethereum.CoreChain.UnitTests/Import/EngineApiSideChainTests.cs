using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Rpc;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs.Engine;
using Xunit;
using EngineWithdrawal = Nethereum.RPC.Eth.DTOs.Withdrawal;

namespace Nethereum.CoreChain.UnitTests.Import
{
    public class EngineApiSideChainTests
    {
        private const string FeeRecipient = "0x0000000000000000000000000000000000009999";
        private static readonly string ZeroHash32 = "0x" + new string('0', 64);
        private static readonly BigInteger Transferred = 100;

        private static IEngineApiService EngineFor(SideChainHarness harness) =>
            new EngineApiService(
                harness.Node.Blocks, harness.Importer, harness.Node.BlockManager.BlockProducer,
                new DifficultyForkChoice(harness.Node.Blocks), new PayloadBuildRegistry(), harness.Node.Config);

        private static ForkchoiceStateV1 StateFor(string hashHex) => new ForkchoiceStateV1
        {
            HeadBlockHash = hashHex,
            SafeBlockHash = hashHex,
            FinalizedBlockHash = hashHex
        };

        private static async Task<ExecutionPayloadV3> ProduceNextPayloadAsync(
            SideChainHarness producer, IEngineApiService engine, ISignedTransaction transfer)
        {
            var head = await producer.Node.Blocks.GetLatestAsync();
            var headHash = await producer.HeadHashAsync();
            var attributes = new PayloadAttributesV3
            {
                Timestamp = new HexBigInteger(head.Timestamp + 12),
                PrevRandao = ZeroHash32,
                SuggestedFeeRecipient = FeeRecipient,
                ParentBeaconBlockRoot = ZeroHash32,
                Withdrawals = new List<EngineWithdrawal>()
            };

            var pending = transfer == null ? new List<ISignedTransaction>() : new List<ISignedTransaction> { transfer };
            var fcu = await engine.ForkchoiceUpdatedAsync(StateFor(headHash.ToHex(true)), attributes, pending);
            return await engine.GetPayloadAsync(fcu.PayloadId);
        }

        private static async Task<List<ExecutionPayloadV3>> ProducePayloadChainAsync(bool withTransfers, int count, long firstTimestampOffset = 0)
        {
            using var producer = await SideChainHarness.CreateAsync();
            var engine = EngineFor(producer);
            var payloads = new List<ExecutionPayloadV3>();
            for (var i = 0; i < count; i++)
            {
                var transfer = withTransfers ? BlockPipelineHarness.Transfer(i) : null;
                payloads.Add(await ProduceNextPayloadAsync(producer, engine, transfer));
            }
            return payloads;
        }

        [Fact]
        public async Task Given_ASiblingPayload_When_NewPayload_Then_TheStatusIsAcceptedAndTheHeadIsUnchanged()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 1);
            var b = await ProducePayloadChainAsync(withTransfers: true, 1);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);
            Assert.Equal(EnginePayloadStatus.Valid, (await engine.NewPayloadAsync(a[0], ZeroHash32)).Status);

            var status = await engine.NewPayloadAsync(b[0], ZeroHash32);

            Assert.Equal(EnginePayloadStatus.Accepted, status.Status);
            Assert.Null(status.LatestValidHash);
            Assert.Equal(a[0].BlockHash, (await node.HeadHashAsync()).ToHex(true));
            
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(BlockPipelineHarness.RecipientAddress));
        }

        [Fact]
        public async Task Given_AnAcceptedSibling_When_ForkchoiceUpdatedToIt_Then_TheStatusIsValidAndTheHeadStateRootIsTheSiblings()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 1);
            var b = await ProducePayloadChainAsync(withTransfers: true, 1);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);
            await engine.NewPayloadAsync(a[0], ZeroHash32);
            await engine.NewPayloadAsync(b[0], ZeroHash32);

            var fcu = await engine.ForkchoiceUpdatedAsync(StateFor(b[0].BlockHash), null);

            Assert.Equal(EnginePayloadStatus.Valid, fcu.PayloadStatus.Status);
            Assert.Equal(b[0].BlockHash, fcu.PayloadStatus.LatestValidHash);
            Assert.Equal(b[0].BlockHash, (await node.HeadHashAsync()).ToHex(true));
            Assert.Equal(b[0].StateRoot, (await node.Node.Blocks.GetLatestAsync()).StateRoot.ToHex(true));
            Assert.Equal(Transferred, await node.BalanceOfAsync(BlockPipelineHarness.RecipientAddress));
        }

        [Fact]
        public async Task Given_AnAdoptedSibling_When_ForkchoiceUpdatedBackToTheOldHead_Then_TheStatusIsValid()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 1);
            var b = await ProducePayloadChainAsync(withTransfers: true, 1);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);
            await engine.NewPayloadAsync(a[0], ZeroHash32);
            await engine.NewPayloadAsync(b[0], ZeroHash32);
            await engine.ForkchoiceUpdatedAsync(StateFor(b[0].BlockHash), null);

            var back = await engine.ForkchoiceUpdatedAsync(StateFor(a[0].BlockHash), null);

            Assert.Equal(EnginePayloadStatus.Valid, back.PayloadStatus.Status);
            Assert.Equal(a[0].BlockHash, (await node.HeadHashAsync()).ToHex(true));
            
            Assert.Equal(BigInteger.Zero, await node.BalanceOfAsync(BlockPipelineHarness.RecipientAddress));
        }

        [Fact]
        public async Task Given_ASideBranchWhoseSecondBlockIsInvalid_When_ForkchoiceUpdatedToItsTip_Then_TheStatusIsInvalidWithLatestValidHashOfTheFirstBlock()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 2);
            using var producerB = await SideChainHarness.CreateAsync(withSideChain: false);
            var b = await producerB.ProduceChainAsync(SideChainHarness.RecipientB, 2);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);
            foreach (var payload in a) await engine.NewPayloadAsync(payload, ZeroHash32);
            await node.ImportAsync(b[0]);
            var forgedHeader = BlockPipelineHarness.CloneHeader(b[1].Header);
            forgedHeader.StateRoot = new byte[32];
            forgedHeader.StateRoot[0] = 9;
            var forged = await node.Importer.ImportAsync(
                forgedHeader, b[1].Transactions, uncles: null, b[1].Withdrawals, CancellationToken.None);

            var fcu = await engine.ForkchoiceUpdatedAsync(StateFor(forged.BlockHash.ToHex(true)), null);

            Assert.Equal(EnginePayloadStatus.Invalid, fcu.PayloadStatus.Status);
            Assert.Equal(b[0].Hash.ToHex(true), fcu.PayloadStatus.LatestValidHash);
        }

        [Fact]
        public async Task Given_APayloadRejectedOnce_When_SubmittedAgain_Then_ItStaysInvalid()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 1);
            a[0].ExtraData = "0x" + new string('1', 66);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);

            var first = await engine.NewPayloadAsync(a[0], ZeroHash32);
            var second = await engine.NewPayloadAsync(a[0], ZeroHash32);

            Assert.Equal(EnginePayloadStatus.Invalid, first.Status);
            Assert.Equal(EnginePayloadStatus.Invalid, second.Status);
            Assert.Equal(first.LatestValidHash, second.LatestValidHash);
        }

        [Fact]
        public async Task Given_AKnownCanonicalPayload_When_NewPayload_Then_TheStatusIsValidWithItsOwnHash()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 1);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);
            await engine.NewPayloadAsync(a[0], ZeroHash32);

            var again = await engine.NewPayloadAsync(a[0], ZeroHash32);

            Assert.True(again.Status == EnginePayloadStatus.Valid, again.ValidationError);
            Assert.Equal(a[0].BlockHash, again.LatestValidHash);
        }

        [Fact]
        public async Task Given_AForkchoiceToAHeadBeyondMaxReorgDepth_When_Called_Then_AnErrorObjectIsReturned()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, 3);
            var b = await ProducePayloadChainAsync(withTransfers: true, 1);
            using var node = await SideChainHarness.CreateAsync(maxReorgDepth: 2);
            var engine = EngineFor(node);
            foreach (var payload in a) await engine.NewPayloadAsync(payload, ZeroHash32);
            Assert.Equal(EnginePayloadStatus.Accepted, (await engine.NewPayloadAsync(b[0], ZeroHash32)).Status);

            var error = await Assert.ThrowsAsync<RpcException>(
                () => engine.ForkchoiceUpdatedAsync(StateFor(b[0].BlockHash), null));

            Assert.Equal(-38006, error.Code);
            Assert.Equal(a[2].BlockHash, (await node.HeadHashAsync()).ToHex(true));
        }

        [Fact]
        public async Task Given_AFinalizedCanonicalBlock_When_ForkchoiceUpdated_Then_SideBlocksBelowItAreDropped()
        {
            var a = await ProducePayloadChainAsync(withTransfers: false, count: 2);
            var b = await ProducePayloadChainAsync(withTransfers: true, count: 1);
            using var node = await SideChainHarness.CreateAsync();
            var engine = EngineFor(node);
            foreach (var payload in a) await engine.NewPayloadAsync(payload, ZeroHash32);
            Assert.Equal(EnginePayloadStatus.Accepted, (await engine.NewPayloadAsync(b[0], ZeroHash32)).Status);
            Assert.True(node.SideChain.SideBlocks.Contains(b[0].BlockHash.HexToByteArray()));

            var fcu = await engine.ForkchoiceUpdatedAsync(StateFor(a[1].BlockHash), null);

            Assert.Equal(EnginePayloadStatus.Valid, fcu.PayloadStatus.Status);
            Assert.False(node.SideChain.SideBlocks.Contains(b[0].BlockHash.HexToByteArray()));
        }
    }
}
