using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Model.P2P;
using Nethereum.Model.P2P.Snap;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Model.UnitTests
{
    public class S1aR7ReviewTests
    {
        private readonly ITestOutputHelper _out;

        public S1aR7ReviewTests(ITestOutputHelper output)
        {
            _out = output;
        }

        private static byte[] E(params byte[] bytes) => RLP.RLP.EncodeElement(bytes);
        private static byte[] Filled(byte value, int length) => Enumerable.Repeat(value, length).ToArray();
        private static readonly byte[] EmptyList = RLP.RLP.EncodeList();

        private static byte[] MinimalLegacyTx() =>
            new byte[] { 0xc9, 0x80, 0x80, 0x80, 0x80, 0x80, 0x80, 0x1b, 0x01, 0x01 };

        private static byte[] TransactionsMessage(int count)
        {
            return RLP.RLP.EncodeList(Enumerable.Range(1, count).Select(DistinctLegacyTx).ToArray());
        }

        private static byte[] DistinctLegacyTx(int nonce) =>
            RLP.RLP.EncodeList(E(1, (byte)(nonce >> 16), (byte)(nonce >> 8), (byte)nonce), E(), E(), E(), E(), E(), E(0x1b), E(1), E(1));

        private static byte[] TypedWire(byte type, byte[][] fields) =>
            RLP.RLP.EncodeElement(new[] { type }.Concat(RLP.RLP.EncodeList(fields)).ToArray());

        private static byte[][] Fields7702(byte[] to, byte[] gas)
        {
            return new[]
            {
                E(1), E(), E(1), E(2), E(gas), E(to), E(), E(0x12, 0x34), EmptyList, EmptyList,
                E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32))
            };
        }

        private void Report(string name, int payloadLength, long allocated)
            => _out.WriteLine($"{name}: payload={payloadLength} allocated={allocated} ratio={allocated / (double)payloadLength:F1}");

        [Fact]
        public void Measure_Amplification()
        {
            var payload = TransactionsMessage(PeerResponseBounds.MaxTransactionAnnouncements);
            var before = GC.GetAllocatedBytesForCurrentThread();
            TransactionsMessageEncoder.Decode(payload);
            Report("transactions", payload.Length, GC.GetAllocatedBytesForCurrentThread() - before);

            var item = RLP.RLP.EncodeList(E(Filled(0x11, 20)), EmptyList);
            var accessList = RLP.RLP.EncodeList(Enumerable.Range(0, 100000).Select(_ => item).ToArray());
            var tx = RLP.RLP.EncodeList(E(1), E(), E(1), E(2), E(0x52, 0x08), E(Filled(0x11, 20)), E(), E(0x12, 0x34), accessList,
                E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32)));
            var wire = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(new byte[] { 2 }.Concat(tx).ToArray()));
            before = GC.GetAllocatedBytesForCurrentThread();
            TransactionsMessageEncoder.Decode(wire);
            Report("accessList", wire.Length, GC.GetAllocatedBytesForCurrentThread() - before);

            var log = RLP.RLP.EncodeList(E(Filled(0x11, 20)), EmptyList, E());
            var logs = RLP.RLP.EncodeList(Enumerable.Range(0, 100000).Select(_ => log).ToArray());
            var receipt = RLP.RLP.EncodeList(E(1), E(0x52, 0x08), E(new byte[256]), logs);
            var receipts = RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(RLP.RLP.EncodeList(receipt)));
            before = GC.GetAllocatedBytesForCurrentThread();
            ReceiptsMessageEncoder.Decode(receipts);
            Report("logs", receipts.Length, GC.GetAllocatedBytesForCurrentThread() - before);

            var account = RLP.RLP.EncodeList(E(Filled(0x11, 20)), EmptyList, EmptyList, EmptyList, EmptyList, EmptyList);
            var bal = RLP.RLP.EncodeList(Enumerable.Range(0, 100000).Select(_ => account).ToArray());
            before = GC.GetAllocatedBytesForCurrentThread();
            BlockAccessListRLPEncoder.Current.Decode(bal);
            Report("bal", bal.Length, GC.GetAllocatedBytesForCurrentThread() - before);

            var slim = RLP.RLP.EncodeList(E(1), E(2), E(), E());
            var accounts = RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(Enumerable.Range(0, 100000)
                .Select(_ => RLP.RLP.EncodeList(E(Filled(0x11, 32)), slim)).ToArray()),
                EmptyList);
            before = GC.GetAllocatedBytesForCurrentThread();
            AccountRangeMessageEncoder.Decode(accounts);
            Report("accountRange", accounts.Length, GC.GetAllocatedBytesForCurrentThread() - before);
        }

        [Fact]
        public void S1aR7_01_Given_ATransactionsBroadcastOfFiveThousandAndOneTransactions_When_Decoded_Then_ItIsRefused()
        {
            var payload = TransactionsMessage(5001);

            Assert.ThrowsAny<Exception>(() => TransactionsMessageEncoder.Decode(payload));
        }

        [Fact]
        public void S1aR7_01_Given_AOneMebibyteTransactionsBroadcast_When_Decoded_Then_AllocationStaysWithinSixteenTimesThePayload()
        {
            var payload = TransactionsMessage(100000);
            var before = GC.GetAllocatedBytesForCurrentThread();
            try
            {
                TransactionsMessageEncoder.Decode(payload);
            }
            catch
            {
            }
            var allocated = GC.GetAllocatedBytesForCurrentThread() - before;

            Assert.True(allocated <= 16L * payload.Length,
                $"{allocated} bytes allocated for a {payload.Length} byte payload ({allocated / (double)payload.Length:F1}x)");
        }

        public static IEnumerable<object[]> NonCanonicalBalValues()
        {
            yield return new object[] { "balance with a leading zero", 2, new byte[] { 0x00, 0x01 } };
            yield return new object[] { "balance of 33 bytes", 2, Filled(0x01, 33) };
            yield return new object[] { "nonce with a leading zero", 3, new byte[] { 0x00, 0x01 } };
            yield return new object[] { "nonce of 9 bytes", 3, Filled(0x01, 9) };
        }

        [Theory]
        [MemberData(nameof(NonCanonicalBalValues))]
        public void S1aR7_03_Given_ABlockAccessListWithANonCanonicalValue_When_Decoded_Then_ItIsRefusedWithAnInvalidOperation(
            string name, int listIndex, byte[] value)
        {
            var change = RLP.RLP.EncodeList(E(1), E(value));
            var lists = new[] { EmptyList, EmptyList, EmptyList, EmptyList, EmptyList };
            lists[listIndex] = RLP.RLP.EncodeList(change);
            var account = RLP.RLP.EncodeList(new[] { E(Filled(0x11, 20)) }.Concat(lists).ToArray());
            var bal = RLP.RLP.EncodeList(account);

            Assert.Throws<InvalidOperationException>(() => BlockAccessListRLPEncoder.Current.Decode(bal));
        }

        [Fact]
        public void S1aR7_03_Given_ABlockAccessListWithAnIndexAboveUint32_When_Decoded_Then_ItIsRefused()
        {
            var change = RLP.RLP.EncodeList(E(1, 0, 0, 0, 0), E(1));
            var account = RLP.RLP.EncodeList(E(Filled(0x11, 20)), EmptyList, EmptyList, RLP.RLP.EncodeList(change), EmptyList, EmptyList);
            var bal = RLP.RLP.EncodeList(account);

            Assert.ThrowsAny<Exception>(() => BlockAccessListRLPEncoder.Current.Decode(bal));
        }

        [Theory]
        [InlineData(3)]
        [InlineData(4)]
        public void S1aR7_04_Given_ATypedTransactionWithAnEmptyTo_When_ReadFromTheWire_Then_ItIsRefusedAsGethRequiresAnAddress(int type)
        {
            byte[] wire;
            if (type == 4)
                wire = TypedWire(4, Fields7702(new byte[0], new byte[] { 0x52, 0x08 }));
            else
            {
                var fields = new[]
                {
                    E(1), E(), E(1), E(2), E(0x52, 0x08), E(), E(), E(0x12, 0x34), EmptyList,
                    E(1), RLP.RLP.EncodeList(E(Filled(0x44, 32))), E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32))
                };
                wire = TypedWire(3, fields);
            }

            var reader = new RlpReader(wire);

            Assert.ThrowsAny<Exception>(() => TransactionFactory.ReadTransaction(reader));
        }

        [Fact]
        public void S1aR7_04_Given_ATypedTransactionWithAGasLimitWiderThanUint64_When_ReadFromTheWire_Then_ItIsRefused()
        {
            var fields = new[]
            {
                E(1), E(), E(1), E(2), E(Filled(0x01, 9)), E(Filled(0x11, 20)), E(), E(0x12, 0x34), EmptyList,
                E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32))
            };
            var reader = new RlpReader(TypedWire(2, fields));

            Assert.ThrowsAny<Exception>(() => TransactionFactory.ReadTransaction(reader));
        }

        [Theory]
        [InlineData(0x00)]
        [InlineData(0x02)]
        [InlineData(0x05)]
        public void S1aR7_05_Given_AReceiptWhoseStatusIsASingleByteOtherThanOne_When_Decoded_Then_ItIsRefused(byte status)
        {
            var receipt = RLP.RLP.EncodeList(E(status), E(0x52, 0x08), E(new byte[256]), EmptyList);

            Assert.ThrowsAny<Exception>(() => ReceiptEncoder.Current.Decode(receipt));
        }

        [Fact]
        public void S1aR7_05_Given_ATypedReceiptOfAnUnsupportedType_When_Decoded_Then_ItIsRefused()
        {
            var receipt = new byte[] { 0x05 }.Concat(RLP.RLP.EncodeList(E(1), E(0x52, 0x08), E(new byte[256]), EmptyList)).ToArray();

            Assert.ThrowsAny<Exception>(() => ReceiptEncoder.Current.Decode(receipt));
        }

        [Fact]
        public void S1aR7_06_Given_ATransactionsBroadcastCarryingABlobTransactionWithItsSidecar_When_Decoded_Then_ItIsRefused()
        {
            var inner = RLP.RLP.EncodeList(
                E(1), E(), E(1), E(2), E(0x52, 0x08), E(Filled(0x11, 20)), E(), E(0x12, 0x34), EmptyList,
                E(1), RLP.RLP.EncodeList(E(Filled(0x44, 32))), E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32)));
            var network = new byte[] { 3 }.Concat(RLP.RLP.EncodeList(
                inner,
                RLP.RLP.EncodeList(E(new byte[131072])),
                RLP.RLP.EncodeList(E(new byte[48])),
                RLP.RLP.EncodeList(E(new byte[48])))).ToArray();
            var payload = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(network));

            Assert.ThrowsAny<Exception>(() => TransactionsMessageEncoder.Decode(payload));
        }

        [Fact]
        public void S1aR7_07_Given_ABlockBodiesResponseWhoseFirstBodyHoldsMoreTransactionsThanABlockCanFit_When_Decoded_Then_ItIsRefusedNotSilentlyTruncated()
        {
            var tx = MinimalLegacyTx();
            var txs = RLP.RLP.EncodeList(Enumerable.Range(0, PeerResponseBounds.MaxTransactionsPerBlock + 1).Select(_ => tx).ToArray());
            var body = RLP.RLP.EncodeList(txs, EmptyList);
            var good = RLP.RLP.EncodeList(EmptyList, EmptyList);
            var payload = RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(body, good));

            var decoded = default(BlockBodiesMessage);
            Exception thrown = null;
            try { decoded = BlockBodiesMessageEncoder.Decode(payload); } catch (Exception ex) { thrown = ex; }

            Assert.True(thrown != null, "the response was accepted with " + decoded?.Bodies.Count + " of 2 bodies");
        }

        [Fact]
        public void S1aR7_08_Given_AGetTrieNodesRequestWhoseSkippedPathsHoldAList_When_Decoded_Then_ItIsRefused()
        {
            var first = RLP.RLP.EncodeList(Enumerable.Range(0, 1024).Select(_ => E(1)).ToArray());
            var second = RLP.RLP.EncodeList(RLP.RLP.EncodeList(E(1)));
            var payload = RLP.RLP.EncodeList(E(1), E(Filled(0x01, 32)), RLP.RLP.EncodeList(first, second), E(1));

            Assert.ThrowsAny<Exception>(() => GetTrieNodesMessageEncoder.Decode(payload));
        }
    }
}
