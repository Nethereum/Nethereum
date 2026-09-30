using System;
using Xunit;

namespace Nethereum.Model.UnitTests
{
    public class LegacyTransactionScalarRefusalTests
    {
        private const byte LeadingZero = 0x00;

        private static byte[] SignatureComponent() => Filled(32, 0x11);

        private static byte[] Filled(int length, byte with)
        {
            var bytes = new byte[length];
            for (var i = 0; i < length; i++) bytes[i] = with;
            return bytes;
        }

        private static byte[] AddressStartingWith(byte first)
        {
            var address = Filled(20, 0x22);
            address[0] = first;
            return address;
        }

        private static byte[] SignedLegacyRlp(
            byte[] nonce = null, byte[] value = null, byte[] receiveAddress = null,
            byte[] data = null, byte[] v = null, byte[] gasLimit = null)
            => Nethereum.RLP.RLP.EncodeList(
                Nethereum.RLP.RLP.EncodeElement(nonce ?? new byte[] { 0x01 }),
                Nethereum.RLP.RLP.EncodeElement(new byte[] { 0x04, 0xa8, 0x17, 0xc8, 0x00 }),
                Nethereum.RLP.RLP.EncodeElement(gasLimit ?? new byte[] { 0x52, 0x08 }),
                Nethereum.RLP.RLP.EncodeElement(receiveAddress ?? AddressStartingWith(0x22)),
                Nethereum.RLP.RLP.EncodeElement(value ?? new byte[] { 0x0a }),
                Nethereum.RLP.RLP.EncodeElement(data ?? Array.Empty<byte>()),
                Nethereum.RLP.RLP.EncodeElement(v ?? new byte[] { 0x1b }),
                Nethereum.RLP.RLP.EncodeElement(SignatureComponent()),
                Nethereum.RLP.RLP.EncodeElement(SignatureComponent()));

        [Fact]
        public void Given_ANonceWithALeadingZero_When_Decoded_Then_ItIsRefusedNamingTheNonce()
        {
            var refusal = Assert.Throws<NonCanonicalScalarRlpException>(
                () => TransactionFactory.CreateTransaction(SignedLegacyRlp(nonce: new byte[] { LeadingZero, 0x01 })));

            Assert.Equal(LegacyTransactionField.Nonce, refusal.Field);
        }

        [Fact]
        public void Given_AValueWithALeadingZero_When_Decoded_Then_ItIsRefusedNamingTheValue()
        {
            var refusal = Assert.Throws<NonCanonicalScalarRlpException>(
                () => TransactionFactory.CreateTransaction(SignedLegacyRlp(value: new byte[] { LeadingZero, 0x0a })));

            Assert.Equal(LegacyTransactionField.Value, refusal.Field);
        }

        [Fact]
        public void Given_AValueOneByteWiderThanTheField_When_Decoded_Then_ItIsRefusedNamingTheValue()
        {
            var refusal = Assert.Throws<ScalarWiderThanItsFieldException>(
                () => TransactionFactory.CreateTransaction(SignedLegacyRlp(value: Filled(33, 0x01))));

            Assert.Equal(LegacyTransactionField.Value, refusal.Field);
            Assert.Equal(33, refusal.ByteCount);
        }

        [Fact]
        public void Given_AValueFillingTheFieldExactly_When_Decoded_Then_ItIsAccepted()
        {
            var tx = TransactionFactory.CreateTransaction(SignedLegacyRlp(value: Filled(32, 0x01)));

            Assert.NotNull(tx);
        }

        [Fact]
        public void Given_AGasLimitWiderThanTwoHundredFiftySixBits_When_Decoded_Then_ItIsNotRefused()
        {
            var tx = TransactionFactory.CreateTransaction(SignedLegacyRlp(gasLimit: Filled(33, 0x01)));

            Assert.NotNull(tx);
        }

        [Fact]
        public void Given_AnAddressAndCalldataBeginningWithAZeroByte_When_Decoded_Then_TheyAreNotTreatedAsScalars()
        {
            var tx = TransactionFactory.CreateTransaction(
                SignedLegacyRlp(receiveAddress: AddressStartingWith(LeadingZero),
                                data: new byte[] { LeadingZero, 0xff }));

            Assert.NotNull(tx);
        }

        [Fact]
        public void Given_AnAbsentVMeaningYParityZero_When_Decoded_Then_TheSynthesisedZeroByteSurvivesTheScalarRule()
        {
            var tx = TransactionFactory.CreateTransaction(SignedLegacyRlp(v: Array.Empty<byte>()));

            Assert.Equal(new byte[] { 0x00 }, tx.Signature.V);
        }
    }
}
