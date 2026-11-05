using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    [Collection(HeavySecurityCollection.Name)]
    public class RlpPeerMessageResidualAmplificationSecurityTests
    {
        private const int MaxAllocatedBytesPerPayloadByte = 20;
        private const int MaxFixedShapeAllocatedBytesPerPayloadByte = 4;
        private const int ConcurrentPeers = 3;
        private const int PayloadBytes = 4 * 1024 * 1024;

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public async Task Given_ConcurrentPeersEachSendingAMessageOfNestedListsThatPassesTheElementBudget_When_TheyAreDecoded_Then_TotalAllocationStaysWithinTwentyTimesThePayloads()
        {
            var payload = BudgetPassingNestedListFlood(PayloadBytes);
            var start = new ManualResetEventSlim(false);

            var allocations = Enumerable.Range(0, ConcurrentPeers).Select(_ => Task.Factory.StartNew(() =>
            {
                start.Wait();
                var before = GC.GetAllocatedBytesForCurrentThread();
                try { TransactionsMessageEncoder.Decode(payload); } catch (Exception) { }
                return GC.GetAllocatedBytesForCurrentThread() - before;
            }, TaskCreationOptions.LongRunning)).ToArray();

            start.Set();
            var total = (await Task.WhenAll(allocations)).Sum();
            var allowed = (long)MaxAllocatedBytesPerPayloadByte * payload.Length * ConcurrentPeers;

            Assert.True(total <= allowed,
                $"{ConcurrentPeers} concurrent decodes of a {payload.Length} byte message allocated {total} bytes ({total / ((long)payload.Length * ConcurrentPeers)}x per payload byte), allowed {allowed}");
        }

        [Theory]
        [InlineData("Eth68Status")]
        [InlineData("Eth69Status")]
        [InlineData("BlockRangeUpdate")]
        [InlineData("GetAccountRange")]
        [Trait("Category", "SecurityFinding")]
        public void Given_AFixedShapeMessageCarryingMillionsOfElements_When_ItIsDecoded_Then_AllocationStaysWithinAFewTimesThePayload(string message)
        {
            var payload = BudgetPassingNestedListFlood(PayloadBytes);

            var before = GC.GetAllocatedBytesForCurrentThread();
            try { FixedShapeDecoders[message](payload); } catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated <= (long)MaxFixedShapeAllocatedBytesPerPayloadByte * payload.Length,
                $"decoding a {payload.Length} byte {message} allocated {allocated} bytes ({allocated / payload.Length}x)");
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_AnAccountRangeEntryWhoseBodyIsAStringHoldingNestedRlp_When_TheClientNormalisesItBeforeProofVerification_Then_AllocationStaysWithinTwentyTimesThePayload()
        {
            var nested = BudgetPassingNestedListFlood(PayloadBytes);
            var body = RLP.RLP.EncodeElement(nested);
            var entry = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(new byte[32]), body);
            var message = RLP.RLP.EncodeList(
                new byte[] { 0x01 },
                RLP.RLP.EncodeList(entry),
                RLP.RLP.EncodeList());

            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                var decoded = AccountRangeMessageEncoder.Decode(message);
                foreach (var account in decoded.Accounts)
                    SlimAccountEncoder.FromSlim(account.Body);
            }
            catch (Exception) { }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated <= (long)MaxAllocatedBytesPerPayloadByte * message.Length,
                $"an AccountRange message of {message.Length} bytes allocated {allocated} bytes ({allocated / message.Length}x)");
        }

        private static readonly System.Collections.Generic.Dictionary<string, Func<byte[], object>> FixedShapeDecoders = new()
        {
            ["Eth68Status"] = p => Eth68StatusMessageEncoder.Decode(p),
            ["Eth69Status"] = p => Eth69StatusMessageEncoder.Decode(p),
            ["BlockRangeUpdate"] = p => BlockRangeUpdateMessageEncoder.Decode(p),
            ["GetAccountRange"] = p => GetAccountRangeMessageEncoder.Decode(p)
        };

        private static byte[] BudgetPassingNestedListFlood(int targetBytes)
        {
            var chain = new byte[] { 0xc0 };
            for (var i = 1; i < 54; i++)
                chain = new[] { (byte)(0xc0 + chain.Length) }.Concat(chain).ToArray();
            var block = chain.Concat(new byte[] { 0x93 }).Concat(Enumerable.Repeat((byte)0xaa, 19)).ToArray();

            var count = (targetBytes - 16) / block.Length;
            var content = new MemoryStream(count * block.Length);
            for (var i = 0; i < count; i++) content.Write(block, 0, block.Length);

            var inner = RLP.RLP.EncodeList(content.ToArray());
            return RLP.RLP.EncodeList(new byte[] { 0x01 }, inner);
        }
    }
}
