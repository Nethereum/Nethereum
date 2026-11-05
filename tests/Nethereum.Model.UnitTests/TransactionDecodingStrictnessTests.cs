using System;
using System.Linq;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class TransactionDecodingStrictnessTests
    {
        private static byte[] Filled(int length, byte with) => Enumerable.Repeat(with, length).ToArray();

        private static byte[] Element(byte[] value) => Nethereum.RLP.RLP.EncodeElement(value);

        private static byte[] List(params byte[][] items) => Nethereum.RLP.RLP.EncodeList(items);

        private static byte[][] WellFormedAuthorisationFields() => new[]
        {
            Element(new byte[] { 0x01 }),
            Element(Filled(20, 0x22)),
            Element(new byte[] { 0x07 }),
            Element(new byte[] { 0x01 }),
            Element(Filled(32, 0x11)),
            Element(Filled(32, 0x12))
        };

        private static byte[] SetCodeTransaction(byte[] authorisation, byte[] nonce = null)
        {
            var payload = List(
                Element(new byte[] { 0x01 }),
                Element(nonce ?? new byte[] { 0x02 }),
                Element(new byte[] { 0x01 }),
                Element(new byte[] { 0x0a }),
                Element(new byte[] { 0x52, 0x08 }),
                Element(Filled(20, 0x33)),
                Element(Array.Empty<byte>()),
                Element(Array.Empty<byte>()),
                List(),
                List(authorisation),
                Element(new byte[] { 0x01 }),
                Element(Filled(32, 0x11)),
                Element(Filled(32, 0x12)));
            return new byte[] { 0x04 }.Concat(payload).ToArray();
        }

        private static byte[] Authorisation(Func<byte[][], byte[][]> change) => List(change(WellFormedAuthorisationFields()));

        private static byte[][] Replace(byte[][] fields, int index, byte[] with)
        {
            fields[index] = with;
            return fields;
        }

        [Fact]
        public void Given_AWellFormedSetCodeTransaction_When_Decoded_Then_ItsAuthorisationIsRead()
        {
            var tx = (Transaction7702)TransactionFactory.CreateTransaction(SetCodeTransaction(Authorisation(f => f)));

            var authorisation = Assert.Single(tx.AuthorisationList);
            Assert.Equal(new Nethereum.Util.EvmUInt256(7), authorisation.Nonce);
            Assert.Equal(new byte[] { 0x01 }, authorisation.V);
        }

        [Fact]
        public void Given_AnAuthorisationWithASeventhElement_When_Decoded_Then_ItIsRefusedAsAMalformedAuthorisation()
        {
            var authorisation = Authorisation(f => f.Concat(new[] { Element(new byte[] { 0x01 }) }).ToArray());

            Assert.Throws<AuthorisationFormatException>(() => TransactionFactory.CreateTransaction(SetCodeTransaction(authorisation)));
        }

        [Fact]
        public void Given_AnAuthorisationNonceEncodedAsAList_When_Decoded_Then_ItIsRefusedAsAMalformedAuthorisation()
        {
            var authorisation = Authorisation(f => Replace(f, 2, List(Element(new byte[] { 0x07 }))));

            Assert.Throws<AuthorisationFormatException>(() => TransactionFactory.CreateTransaction(SetCodeTransaction(authorisation)));
        }

        [Fact]
        public void Given_AnAuthorisationChainIdWithALeadingZeroByte_When_Decoded_Then_ItIsRefusedAsAMalformedAuthorisation()
        {
            var authorisation = Authorisation(f => Replace(f, 0, Element(new byte[] { 0x00, 0x01 })));

            Assert.Throws<AuthorisationFormatException>(() => TransactionFactory.CreateTransaction(SetCodeTransaction(authorisation)));
        }

        [Fact]
        public void Given_AnAuthorisationYParityOf256_When_Decoded_Then_ItIsRefusedAsAMalformedAuthorisation()
        {
            var authorisation = Authorisation(f => Replace(f, 3, Element(new byte[] { 0x01, 0x00 })));

            Assert.Throws<AuthorisationFormatException>(() => TransactionFactory.CreateTransaction(SetCodeTransaction(authorisation)));
        }

        [Fact]
        public void Given_AnAuthorisationRWiderThan256Bits_When_Decoded_Then_ItIsRefusedAsAMalformedAuthorisation()
        {
            var authorisation = Authorisation(f => Replace(f, 4, Element(Filled(33, 0x11))));

            Assert.Throws<AuthorisationFormatException>(() => TransactionFactory.CreateTransaction(SetCodeTransaction(authorisation)));
        }

        [Fact]
        public void Given_ATransactionNonceOf2Pow64_When_Decoded_Then_ItIsRefusedAsANonceOverflow()
        {
            var nonce = new byte[] { 0x01, 0, 0, 0, 0, 0, 0, 0, 0 };

            Assert.Throws<TransactionNonceOverflowException>(
                () => TransactionFactory.CreateTransaction(SetCodeTransaction(Authorisation(f => f), nonce)));
        }

        [Fact]
        public void Given_BytesAfterTheTransactionRlp_When_Decoded_Then_ItIsRefused()
        {
            var withTrailingByte = SetCodeTransaction(Authorisation(f => f)).Concat(new byte[] { 0x00 }).ToArray();

            Assert.Throws<TransactionRlpLengthException>(() => TransactionFactory.CreateTransaction(withTrailingByte));
        }
    }
}
