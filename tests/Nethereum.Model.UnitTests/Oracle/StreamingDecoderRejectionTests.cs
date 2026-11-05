using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Codecs;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.Model.UnitTests
{
    public class StreamingDecoderRejectionTests
    {
        private readonly ITestOutputHelper _output;

        public StreamingDecoderRejectionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static byte[] E(params byte[] bytes) => RLP.RLP.EncodeElement(bytes);
        private static byte[] Filled(byte value, int length) => Enumerable.Repeat(value, length).ToArray();
        private static readonly byte[] EmptyList = RLP.RLP.EncodeList();

        private static byte[][] Fields1559()
        {
            return new[]
            {
                E(1), E(), E(1), E(2), E(0x52, 0x08), E(Filled(0x11, 20)), E(), E(0x12, 0x34), EmptyList,
                E(1), E(Filled(0x22, 32)), E(Filled(0x33, 32))
            };
        }

        private static byte[] Typed(byte type, byte[][] fields)
        {
            return new[] { type }.Concat(RLP.RLP.EncodeList(fields)).ToArray();
        }

        private static byte[][] With(byte[][] fields, int index, byte[] replacement)
        {
            var copy = (byte[][])fields.Clone();
            copy[index] = replacement;
            return copy;
        }

        [Fact]
        public void Given_ValidTypedFields_When_Decoded_Then_TheTransactionIsRead()
        {
            var tx = (Transaction1559)TransactionFactory.CreateTransaction(Typed(2, Fields1559()));

            Assert.Equal("0x1234", tx.Data);
            Assert.Equal(21000, (int)tx.GasLimit.Value);
            Assert.NotNull(tx.Signature);
        }

        [Fact]
        public void Given_ANonceWithALeadingZero_When_ATypedTransactionIsDecoded_Then_ItIsRefusedNamingTheNonce()
        {
            var refusal = Assert.Throws<NonCanonicalScalarRlpException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 1, E(0, 5)))));

            Assert.Equal(LegacyTransactionField.Nonce, refusal.Field);
        }

        [Fact]
        public void Given_AGasLimitOf33Bytes_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            Assert.Throws<ScalarWiderThanItsFieldException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 4, E(Filled(0x01, 33))))));
        }

        [Fact]
        public void Given_AReceiverOf19Bytes_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            Assert.Throws<TransactionFieldFormatException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 5, E(Filled(0x11, 19))))));
        }

        [Fact]
        public void Given_AnAccessListAddressOf19Bytes_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            var item = RLP.RLP.EncodeList(E(Filled(0x11, 19)), EmptyList);
            Assert.Throws<InvalidOperationException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 8, RLP.RLP.EncodeList(item)))));
        }

        [Fact]
        public void Given_AnAccessListStorageKeyOf31Bytes_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            var item = RLP.RLP.EncodeList(E(Filled(0x11, 20)), RLP.RLP.EncodeList(E(Filled(0x22, 31))));
            Assert.Throws<InvalidOperationException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 8, RLP.RLP.EncodeList(item)))));
        }

        [Fact]
        public void Given_AThirteenthField_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            Assert.Throws<InvalidOperationException>(
                () => TransactionFactory.CreateTransaction(Typed(2, Fields1559().Concat(new[] { E(1) }).ToArray())));
        }

        [Fact]
        public void Given_OnlyTheVOfTheSignature_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            Assert.Throws<InvalidOperationException>(
                () => TransactionFactory.CreateTransaction(Typed(2, Fields1559().Take(10).ToArray())));
        }

        [Fact]
        public void Given_ASignatureSWithALeadingZero_When_ATypedTransactionIsDecoded_Then_ItIsRefusedNamingS()
        {
            var refusal = Assert.Throws<NonCanonicalScalarRlpException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 11, E(0, 1, 2)))));

            Assert.Equal(LegacyTransactionField.SignatureS, refusal.Field);
        }

        [Fact]
        public void Given_CalldataGivenAsAList_When_ATypedTransactionIsDecoded_Then_ItIsRefused()
        {
            Assert.Throws<InvalidOperationException>(
                () => TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 7, EmptyList))));
        }

        [Fact]
        public void Given_ALegacyTransactionWithTenFields_When_Decoded_Then_ItIsRefused()
        {
            var fields = new[]
            {
                E(1), E(2), E(3), E(Filled(0x11, 20)), E(4), E(), E(0x1b), E(Filled(0x22, 32)), E(Filled(0x33, 32)), E(1)
            };
            Assert.Throws<InvalidOperationException>(() => TransactionFactory.CreateTransaction(RLP.RLP.EncodeList(fields)));
        }

        [Fact]
        public void Given_ALegacyTransactionWithAReceiverOf19Bytes_When_Decoded_Then_ItIsRefused()
        {
            var fields = new[]
            {
                E(1), E(2), E(3), E(Filled(0x11, 19)), E(4), E(), E(0x1b), E(Filled(0x22, 32)), E(Filled(0x33, 32))
            };
            Assert.Throws<TransactionFieldFormatException>(() => TransactionFactory.CreateTransaction(RLP.RLP.EncodeList(fields)));
        }

        [Fact]
        public void Given_ALegacyTransactionInsideAByteString_When_ReadFromTheWire_Then_ItIsRefused()
        {
            var legacy = RLP.RLP.EncodeList(E(1), E(2), E(3), E(Filled(0x11, 20)), E(4), E(), E(0x1b), E(Filled(0x22, 32)), E(Filled(0x33, 32)));
            var wire = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(legacy));

            var reader = new RlpReader(wire);
            reader.ReadListHeader();
            Assert.Throws<InvalidOperationException>(() => TransactionFactory.ReadTransaction(reader));
        }

        [Fact]
        public void Given_ATypedTransactionInsideAByteString_When_ReadFromTheWire_Then_ItIsDecodedAndKeepsItsOwnBytes()
        {
            var typed = Typed(2, Fields1559());
            var wire = RLP.RLP.EncodeList(RLP.RLP.EncodeElement(typed));

            var reader = new RlpReader(wire);
            var scope = reader.ReadListHeader();
            var tx = TransactionFactory.ReadTransaction(reader);
            reader.EnsureEnd(scope);

            Assert.Equal(typed.ToHex(), tx.GetRLPEncoded().ToHex());
        }

        [Fact]
        public void Given_TheCallersBuffer_When_ATypedTransactionIsCreated_Then_LaterChangesToItDoNotReachTheTransaction()
        {
            var typed = Typed(2, Fields1559());
            var tx = TransactionFactory.CreateTransaction(typed);
            var before = tx.GetRLPEncoded().ToHex();

            typed[typed.Length - 1] ^= 0xff;

            Assert.Equal(before, tx.GetRLPEncoded().ToHex());
        }

        [Theory]
        [InlineData("frontier")]
        [InlineData("london")]
        [InlineData("shanghai")]
        [InlineData("cancun")]
        [InlineData("prague")]
        [InlineData("amsterdam")]
        public void Given_AHeaderOfEachShape_When_ReadFromTheWire_Then_ItEqualsWhatWasEncoded(string shape)
        {
            var header = BlockHeaderShapes.For(shape);
            var message = BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage { RequestId = 77, Headers = { header, header } });

            var decoded = BlockHeadersMessageEncoder.Decode(message);

            Assert.Equal(77UL, decoded.RequestId);
            Assert.Equal(2, decoded.Headers.Count);
            Assert.Equal(BlockHeaderEncoder.Current.Encode(header).ToHex(), BlockHeaderEncoder.Current.Encode(decoded.Headers[1]).ToHex());
        }

        [Fact]
        public void Given_AHeaderWithFourteenFields_When_Decoded_Then_ItIsRefused()
        {
            var fields = Enumerable.Range(0, 14).Select(_ => E(1)).ToArray();
            Assert.Throws<ArgumentException>(() => BlockHeaderEncoder.Current.Decode(RLP.RLP.EncodeList(fields)));
        }

        [Fact]
        public void Given_AHeaderWithABaseFeeLeadingZero_When_Decoded_Then_ItIsRefused()
        {
            var decoded = (RLPCollection)RLP.RLP.Decode(BlockHeaderEncoder.Current.Encode(BlockHeaderShapes.For("london")));
            var fields = new byte[decoded.Count][];
            for (var i = 0; i < decoded.Count; i++) fields[i] = decoded[i].RLPData ?? new byte[0];
            fields[15] = new byte[] { 0, 5 };

            Assert.Throws<InvalidOperationException>(
                () => BlockHeaderEncoder.Current.Decode(RLP.RLP.EncodeDataItemsAsElementOrListAndCombineAsList(fields)));
        }

        [Fact]
        public void Given_AHeadersMessageWithAThirdTopLevelElement_When_Decoded_Then_ItIsRefused()
        {
            var header = BlockHeaderEncoder.Current.Encode(BlockHeaderShapes.For("london"));
            var message = RLP.RLP.EncodeList(E(1), RLP.RLP.EncodeList(header), E(9));

            Assert.Throws<InvalidOperationException>(() => BlockHeadersMessageEncoder.Decode(message));
        }

        [Fact]
        public void Given_ATransactionsMessageWithATrailingByte_When_Decoded_Then_ItIsRefused()
        {
            var message = TransactionsMessageEncoder.Encode(new TransactionsMessage
            {
                Transactions = { TransactionFactory.CreateTransaction(Typed(2, Fields1559())) }
            });

            Assert.Equal(1, TransactionsMessageEncoder.Decode(message).Transactions.Count);
            Assert.Throws<InvalidOperationException>(() => TransactionsMessageEncoder.Decode(message.Concat(new byte[] { 0x01 }).ToArray()));
        }

        [Fact]
        public void Given_APooledTransactionsMessage_When_Decoded_Then_TheRequestIdAndTransactionsAreRead()
        {
            var first = TransactionFactory.CreateTransaction(Typed(2, Fields1559()));
            var second = TransactionFactory.CreateTransaction(Typed(2, With(Fields1559(), 1, E(2))));
            var message = PooledTransactionsMessageEncoder.Encode(new PooledTransactionsMessage
            {
                RequestId = 4242,
                Transactions = { first, second }
            });

            var decoded = PooledTransactionsMessageEncoder.Decode(message);

            Assert.Equal(4242UL, decoded.RequestId);
            Assert.Equal(2, decoded.Transactions.Count);
        }

        private const int MessageBytes = 10 * 1024 * 1024;

        private static byte[] NestedListFlood(int targetBytes)
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

        private static long Allocated(Action action)
        {
            GC.Collect();
            var before = GC.GetAllocatedBytesForCurrentThread();
            try { action(); } catch (Exception) { }
            return GC.GetAllocatedBytesForCurrentThread() - before;
        }

        [Fact]
        public void Given_ATenMebibyteNestedListFlood_When_DecodedAsEachHeavyEthMessage_Then_AllocationStaysWithinThreeTimesTheMessage()
        {
            var flood = NestedListFlood(MessageBytes);
            Assert.True(flood.Length > MessageBytes - 64);

            var transactions = Allocated(() => TransactionsMessageEncoder.Decode(flood));
            var pooled = Allocated(() => PooledTransactionsMessageEncoder.Decode(flood));
            var headers = Allocated(() => BlockHeadersMessageEncoder.Decode(flood));
            _output.WriteLine($"10 MiB nested-list flood: Transactions {transactions} B, PooledTransactions {pooled} B, BlockHeaders {headers} B");

            Assert.True(transactions <= 3L * flood.Length, "Transactions " + transactions);
            Assert.True(pooled <= 3L * flood.Length, "PooledTransactions " + pooled);
            Assert.True(headers <= 3L * flood.Length, "BlockHeaders " + headers);
        }

        [Fact]
        public void Given_ATenMebibyteMessageOfOneValidTransactionFollowedByBinaryJunk_When_Decoded_Then_AllocationStaysWithinThreeTimesTheMessage()
        {
            var typed = Typed(2, Fields1559());
            var item = RLP.RLP.EncodeElement(typed);
            var junk = new byte[MessageBytes];
            for (var i = 0; i < junk.Length; i++) junk[i] = 0xc1;
            var message = RLP.RLP.EncodeList(item, RLP.RLP.EncodeElement(junk));

            var allocated = Allocated(() => TransactionsMessageEncoder.Decode(message));
            _output.WriteLine($"valid tx then 10 MiB string: {allocated} B for {message.Length} B");

            Assert.True(allocated <= 3L * message.Length, "allocated " + allocated);
        }

        [Fact]
        public void Given_ATenMebibyteMessageOfValidTransactions_When_Decoded_Then_AllocationIsReported()
        {
            var tx = TransactionFactory.CreateTransaction(Typed(2, Fields1559()));
            var legacy = TransactionFactory.CreateLegacyTransaction("0x" + Filled(0x11, 20).ToHex(), 21000, 2, 4, "0x", 1,
                "0x" + Filled(0x22, 32).ToHex(), "0x" + Filled(0x33, 32).ToHex(), "0x1b");
            foreach (var sample in new[] { tx, legacy })
            {
                var one = TransactionsMessageEncoder.Encode(new TransactionsMessage { Transactions = { sample } });
                var count = MessageBytes / one.Length;
                var message = TransactionsMessageEncoder.Encode(new TransactionsMessage
                {
                    Transactions = Enumerable.Repeat(sample, count).ToList()
                });

                var allocated = Allocated(() => Assert.Equal(count, TransactionsMessageEncoder.Decode(message).Transactions.Count));
                _output.WriteLine($"{sample.TransactionType}: {count} valid transactions, {message.Length} B message, {allocated} B allocated ({(double)allocated / message.Length:F2}x)");
            }
        }

        [Fact]
        public void Given_ATenMebibyteMessageOfValidHeaders_When_Decoded_Then_AllocationStaysWithinThreeTimesTheMessage()
        {
            var header = BlockHeaderShapes.For("prague");
            var one = BlockHeaderEncoder.Current.Encode(header).Length + 4;
            var count = MessageBytes / one;
            var message = BlockHeadersMessageEncoder.Encode(new BlockHeadersMessage
            {
                RequestId = 1,
                Headers = Enumerable.Repeat(header, count).ToList()
            });

            var allocated = Allocated(() => Assert.Equal(count, BlockHeadersMessageEncoder.Decode(message).Headers.Count));
            _output.WriteLine($"{count} valid headers, {message.Length} B message, {allocated} B allocated ({(double)allocated / message.Length:F2}x)");

            Assert.True(allocated <= 3L * message.Length, "allocated " + allocated);
        }
    }
}
