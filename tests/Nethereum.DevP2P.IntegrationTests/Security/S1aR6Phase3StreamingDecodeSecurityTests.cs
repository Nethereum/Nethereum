using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class S1aR6Phase3StreamingDecodeSecurityTests
    {
        private const int MaxAllocatedBytesPerPayloadByte = 4;

        private static readonly Dictionary<string, Func<byte[], object>> Decoders = new()
        {
            ["BlockBodies"] = p => BlockBodiesMessageEncoder.Decode(p),
            ["Receipts68"] = p => ReceiptsMessageEncoder.Decode(p),
            ["Receipts69"] = p => ReceiptsMessageEth69Encoder.Decode(p),
            ["Receipts70"] = p => ReceiptsMessageEth70Encoder.Decode(p),
            ["NewBlock"] = p => NewBlockMessageEncoder.Decode(p),
            ["NewBlockHashes"] = p => NewBlockHashesMessageEncoder.Decode(p),
            ["EthBlockAccessLists"] = p => Nethereum.Model.P2P.BlockAccessListsMessageEncoder.Decode(p),
            ["SnapBlockAccessLists"] = p => Nethereum.Model.P2P.Snap.BlockAccessListsMessageEncoder.Decode(p),
            ["AccountRange"] = p => AccountRangeMessageEncoder.Decode(p),
            ["StorageRanges"] = p => StorageRangesMessageEncoder.Decode(p),
            ["ByteCodes"] = p => ByteCodesMessageEncoder.Decode(p),
            ["TrieNodes"] = p => TrieNodesMessageEncoder.Decode(p),
            ["BlockAccessList"] = p => BlockAccessListRLPEncoder.Current.Decode(p)
        };

        public static IEnumerable<object[]> Messages() => Decoders.Keys.Select(name => new object[] { name });

        [Theory]
        [MemberData(nameof(Messages))]
        [Trait("Category", "SecurityFinding")]
        public void Given_AMessageOfNestedListsThatPassesTheElementBudget_When_ItIsDecoded_Then_AllocationStaysWithinAFewTimesThePayload(string message)
        {
            var payload = NestedListFlood.BudgetPassing(NestedListFlood.PayloadBytes);

            var before = GC.GetAllocatedBytesForCurrentThread();
            try { Decoders[message](payload); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated <= (long)MaxAllocatedBytesPerPayloadByte * payload.Length,
                $"decoding a {payload.Length} byte {message} allocated {allocated} bytes ({allocated / payload.Length}x)");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AGetTrieNodesRequestOfMillionsOfNonEmptyPathsets_When_ItIsDecoded_Then_OnlyTheServedLookupsAreMaterialisedAndAllocationStaysWithinAFewTimesThePayload()
        {
            var pathset = new byte[] { 0xc1, 0x80 };
            var groups = new byte[(NestedListFlood.PayloadBytes - 64) / pathset.Length * pathset.Length];
            for (var i = 0; i < groups.Length; i += pathset.Length) Buffer.BlockCopy(pathset, 0, groups, i, pathset.Length);
            var payload = RLP.RLP.EncodeList(
                new byte[] { 0x01 },
                RLP.RLP.EncodeElement(new byte[32]),
                RLP.RLP.EncodeList(groups),
                new byte[] { 0x80 });

            var before = GC.GetAllocatedBytesForCurrentThread();
            var decoded = GetTrieNodesMessageEncoder.Decode(payload);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(decoded.Paths.Sum(p => p.Count) <= SnapServingLimits.MaxTrieNodeLookups,
                $"{decoded.Paths.Count} pathsets were materialised");
            Assert.True(allocated <= (long)MaxAllocatedBytesPerPayloadByte * payload.Length,
                $"decoding a {payload.Length} byte GetTrieNodes allocated {allocated} bytes ({allocated / payload.Length}x)");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AGetTrieNodesRequestWithAPathsetOfMillionsOfSlots_When_ItIsDecoded_Then_OnlyTheServedLookupsAreMaterialised()
        {
            var slots = Enumerable.Repeat((byte)0x80, NestedListFlood.PayloadBytes / 2).ToArray();
            var pathset = RLP.RLP.EncodeList(new byte[] { 0x01 }.Concat(slots).ToArray());
            var payload = RLP.RLP.EncodeList(
                new byte[] { 0x01 },
                RLP.RLP.EncodeElement(new byte[32]),
                RLP.RLP.EncodeList(pathset),
                new byte[] { 0x80 });

            var before = GC.GetAllocatedBytesForCurrentThread();
            var decoded = GetTrieNodesMessageEncoder.Decode(payload);
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.Equal(SnapServingLimits.MaxTrieNodeLookups, decoded.Paths.Sum(p => p.Count));
            Assert.True(allocated <= (long)MaxAllocatedBytesPerPayloadByte * payload.Length,
                $"decoding a {payload.Length} byte GetTrieNodes allocated {allocated} bytes ({allocated / payload.Length}x)");
        }
    }
}
