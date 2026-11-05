using System;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.P2P;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class RlpNonCanonicalTransactionSecurityTests
    {
        private static readonly string R = string.Concat(Enumerable.Repeat("11", 32));
        private static readonly string S = string.Concat(Enumerable.Repeat("22", 32));
        private static readonly string To = "94" + string.Concat(Enumerable.Repeat("33", 20));

        private static byte[] LegacyWithNonce(string nonceItemHex)
        {
            var body = nonceItemHex + "09" + "825208" + To + "01" + "80" + "1b" + "a0" + R + "a0" + S;
            var length = body.Length / 2;
            return ("f8" + length.ToString("x2") + body).HexToByteArray();
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ALegacyTransactionWhoseNonceUsesAOneByteStringPrefixForABelow0x80Value_When_Created_Then_ItIsRejectedAsNonCanonicalRlp()
        {
            Assert.NotNull(TransactionFactory.CreateTransaction(LegacyWithNonce("05")));

            Assert.ThrowsAny<Exception>(() => TransactionFactory.CreateTransaction(LegacyWithNonce("8105")));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ALegacyTransactionWhoseListHeaderCarriesALeadingZeroLengthByte_When_Created_Then_ItIsRejectedAsNonCanonicalRlp()
        {
            var canonical = LegacyWithNonce("05");
            var payload = canonical.Skip(2).ToArray();
            var padded = new byte[] { 0xf9, 0x00, (byte)payload.Length }.Concat(payload).ToArray();

            Assert.NotNull(TransactionFactory.CreateTransaction(canonical).Hash);
            Assert.ThrowsAny<Exception>(() => TransactionFactory.CreateTransaction(padded));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ABlockBodiesMessageCarryingALegacyTransactionWithANonCanonicalNonce_When_Decoded_Then_TheMessageIsRejected()
        {
            var tx = LegacyWithNonce("8105");
            var body = Nethereum.RLP.RLP.EncodeList(Nethereum.RLP.RLP.EncodeList(tx), Nethereum.RLP.RLP.EncodeList());
            var message = Nethereum.RLP.RLP.EncodeList(
                Nethereum.RLP.RLP.EncodeElement(new byte[] { 1 }),
                Nethereum.RLP.RLP.EncodeList(body));

            Assert.ThrowsAny<Exception>(() => BlockBodiesMessageEncoder.Decode(message));
        }

        [Fact]
        [Trait("Category", "SecurityFinding")]
        public void Given_ABlockHeaderFollowedByTrailingBytes_When_Decoded_Then_ItIsRejected()
        {
            var header = new BlockHeader
            {
                ParentHash = Enumerable.Repeat((byte)1, 32).ToArray(),
                UnclesHash = Enumerable.Repeat((byte)2, 32).ToArray(),
                Coinbase = "0x" + new string('3', 40),
                StateRoot = Enumerable.Repeat((byte)4, 32).ToArray(),
                TransactionsHash = Enumerable.Repeat((byte)5, 32).ToArray(),
                ReceiptHash = Enumerable.Repeat((byte)6, 32).ToArray(),
                BlockNumber = 7,
                LogsBloom = new byte[256],
                Timestamp = 8,
                GasLimit = 30000000,
                GasUsed = 9,
                MixHash = Enumerable.Repeat((byte)10, 32).ToArray(),
                ExtraData = new byte[0],
                Nonce = new byte[8]
            };
            var encoded = BlockHeaderEncoder.Current.Encode(header);

            Assert.NotNull(BlockHeaderEncoder.Current.Decode(encoded));
            Assert.ThrowsAny<Exception>(() => BlockHeaderEncoder.Current.Decode(encoded.Concat(new byte[] { 0 }).ToArray()));
        }
    }
}
