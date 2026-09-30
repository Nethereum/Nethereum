using System.Collections.Generic;
using System.Linq;
using Nethereum.CoreChain.Freezer.Codecs;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.Freezer.UnitTests
{
    public class BodyClusterItemCodecTests
    {
        private readonly BodyClusterItemCodec _codec = new();

        [Fact]
        public void Given_RealGethBodyItem_When_Decode_Then_HasTransactionsAndNullWithdrawals()
        {
            var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "bodies", compressed: true, itemNumber: 0);

            var body = _codec.Decode(raw);

            Assert.NotEmpty(body.Txs);
            Assert.Null(body.Withdrawals);
        }

        [Fact]
        public void Given_PreShanghaiBody_When_RoundTrip_Then_WithdrawalsNull()
        {
            var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "bodies", compressed: true, itemNumber: 0);

            var body = _codec.Decode(raw);
            var reencoded = _codec.Encode(body);
            var roundTripped = _codec.Decode(reencoded);

            Assert.Null(roundTripped.Withdrawals);
            Assert.Equal(raw, reencoded);
        }

        [Fact]
        public void Given_PostShanghaiZeroWithdrawals_When_RoundTrip_Then_WithdrawalsEmptyNotNull()
        {
            var body = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(),
                new List<Withdrawal>());

            var reencoded = _codec.Encode(body);
            var roundTripped = _codec.Decode(reencoded);

            Assert.NotNull(roundTripped.Withdrawals);
            Assert.Empty(roundTripped.Withdrawals);
        }

        [Fact]
        public void Given_NullVsEmptyWithdrawals_When_Encoded_Then_ProducesDifferentBytes()
        {
            var bodyNull = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(), null);
            var bodyEmpty = new BlockBodyCluster(new List<ISignedTransaction>(), new List<BlockHeader>(),
                new List<Withdrawal>());

            var nullBytes = _codec.Encode(bodyNull);
            var emptyBytes = _codec.Encode(bodyEmpty);

            Assert.NotEqual(nullBytes, emptyBytes);
        }

        [Fact]
        public void Given_RealGethBodyItems_When_DecodeThenReEncode_Then_ByteIdenticalToGeth()
        {
            var count = CorpusFixture.ItemCount(CorpusFixture.TxsSliceDirectory, "bodies", compressed: true);

            for (var item = 0L; item < count; item++)
            {
                var raw = CorpusFixture.ReadRawItem(CorpusFixture.TxsSliceDirectory, "bodies", compressed: true, item);

                var body = _codec.Decode(raw);
                var reencoded = _codec.Encode(body);

                Assert.Equal(raw, reencoded);
            }
        }

        [Fact]
        public void Given_BodyWithTypedTransaction_When_RoundTrip_Then_DecodesIdentically()
        {
            var typedTx = TransactionFactory.Create1559Transaction(
                chainId: 1, nonce: 0, maxPriorityFeePerGas: 1_000_000_000, maxFeePerGas: 2_000_000_000,
                gasLimit: 21000, to: "0x0000000000000000000000000000000000000001", amount: 0, data: "0x",
                accessList: null,
                r: "0x" + new string('1', 64), s: "0x" + new string('2', 64), v: "0x01");

            var body = new BlockBodyCluster(new List<ISignedTransaction> { typedTx },
                new List<BlockHeader>(), null);

            var encoded = _codec.Encode(body);
            var decoded = _codec.Decode(encoded);

            var decodedTx = Assert.Single(decoded.Txs);
            Assert.Equal(TransactionType.EIP1559, decodedTx.TransactionType);
            Assert.Equal(typedTx.GetRLPEncoded(), decodedTx.GetRLPEncoded());
        }
    }
}
