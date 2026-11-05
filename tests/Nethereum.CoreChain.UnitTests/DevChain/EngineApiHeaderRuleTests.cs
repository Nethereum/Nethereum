using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Engine;
using Nethereum.CoreChain.Sync;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs.Engine;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    public class EngineApiHeaderRuleTests
    {
        private const string FeeRecipient = "0x0000000000000000000000000000000000009999";

        private static readonly string ZeroHash32 = "0x" + new string('0', 64);

        private static async Task<(DevChainNode Node, IEngineApiService Engine)> CreateNodeAsync()
        {
            var harness = await Nethereum.CoreChain.UnitTests.Import.SideChainHarness.CreateAsync();
            var node = harness.Node;
            var importer = harness.Importer;
            var engine = new EngineApiService(
                node.Blocks, importer, node.BlockManager.BlockProducer,
                new DifficultyForkChoice(node.Blocks), new PayloadBuildRegistry(), node.Config);
            return (node, engine);
        }

        private static PayloadAttributesV3 NextAttributes(long timestamp) => new PayloadAttributesV3
        {
            Timestamp = new HexBigInteger(timestamp),
            PrevRandao = "0x" + new string('7', 64),
            SuggestedFeeRecipient = FeeRecipient,
            ParentBeaconBlockRoot = ZeroHash32,
            Withdrawals = new List<Nethereum.RPC.Eth.DTOs.Withdrawal>()
        };

        private static async Task<(ExecutionPayloadV3 Payload, PayloadAttributesV3 Attributes, byte[] ParentHash, IEngineApiService Engine)> BuildPayloadAsync()
        {
            var (node, engine) = await CreateNodeAsync();
            var head = await node.Blocks.GetLatestAsync();
            var headHash = await node.Blocks.GetHashByNumberAsync(head.BlockNumber.ToBigInteger());
            var attributes = NextAttributes(head.Timestamp + 1);
            var state = new ForkchoiceStateV1
            {
                HeadBlockHash = headHash.ToHex(true),
                SafeBlockHash = headHash.ToHex(true),
                FinalizedBlockHash = headHash.ToHex(true)
            };
            var fcu = await engine.ForkchoiceUpdatedAsync(state, attributes);
            return (await engine.GetPayloadAsync(fcu.PayloadId), attributes, headHash, engine);
        }

        [Fact]
        public async Task Given_APayloadWith33BytesOfExtraData_When_NewPayloadIsCalled_Then_TheResultIsInvalidNamingExtraDataSizeWithTheParentAsLatestValid()
        {
            var (payload, attributes, parentHash, engine) = await BuildPayloadAsync();
            payload.ExtraData = "0x" + new string('1', 66);

            var status = await engine.NewPayloadAsync(payload, attributes.ParentBeaconBlockRoot);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.Equal(parentHash.ToHex(true), status.LatestValidHash);
            Assert.Contains("extraDataSize", status.ValidationError);
        }

        [Fact]
        public async Task Given_APayloadWithATimestampEqualToItsParents_When_NewPayloadIsCalled_Then_TheResultIsInvalidNamingTimestamp()
        {
            var (payload, attributes, parentHash, engine) = await BuildPayloadAsync();
            payload.Timestamp = new HexBigInteger(payload.Timestamp.Value - 1);

            var status = await engine.NewPayloadAsync(payload, attributes.ParentBeaconBlockRoot);

            Assert.Equal(EnginePayloadStatus.Invalid, status.Status);
            Assert.Equal(parentHash.ToHex(true), status.LatestValidHash);
            Assert.Contains("timestamp", status.ValidationError);
        }
    }
}
