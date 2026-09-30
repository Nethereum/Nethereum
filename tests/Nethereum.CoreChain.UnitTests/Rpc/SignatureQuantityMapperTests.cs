using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class SignatureQuantityMapperTests
    {
        [Theory]
        [InlineData(new byte[] { 0x00 }, "0x0")]
        [InlineData(new byte[] { 0x01 }, "0x1")]
        [InlineData(new byte[] { 0x00, 0x0e, 0xa2 }, "0xea2")]
        public void ToRpcSignatureQuantity_StripsLeadingZeros_And0xPrefixes(byte[] value, string expected)
        {
            Assert.Equal(expected, value.ToRpcSignatureQuantity());
        }

        [Fact]
        public void ToRpcSignatureQuantity_Null_ReturnsNull()
        {
            Assert.Null(((byte[])null).ToRpcSignatureQuantity());
        }

        [Fact]
        public void ToRpcSignatureQuantity_LeftPadded32Byte_StripsToQuantity()
        {
            var padded = new byte[32];
            padded[31] = 0x01;
            Assert.Equal("0x1", padded.ToRpcSignatureQuantity());
        }

        [Fact]
        public void ToRPCAuthorisation_EmitsQuantityEncodedRSYParity()
        {
            var r = new byte[32]; r[1] = 0xe0; r[31] = 0x7a;
            var s = new byte[32]; s[0] = 0x00; s[1] = 0xa2; s[31] = 0x11;
            var signed = new List<Authorisation7702Signed>
            {
                new Authorisation7702Signed(new EvmUInt256(1UL), "0x000000000000000000000000000000000000dEaD",
                    new EvmUInt256(0UL), r, s, new byte[] { 1 })
            };

            var rpc = signed.ToRPCAuthorisation()[0];

            Assert.StartsWith("0x", rpc.R);
            Assert.StartsWith("0x", rpc.S);
            Assert.Equal("0x1", rpc.YParity);
            Assert.Equal(r.ToRpcSignatureQuantity(), rpc.R);
            Assert.False(rpc.R.StartsWith("0x00"));
        }

        [Theory]
        [InlineData((byte)0)]
        [InlineData((byte)1)]
        public void ToRPCAuthorisation_RoundTripThroughSigned_PreservesMagnitudeAndParity(byte parity)
        {
            var r = new byte[32]; r[0] = 0x00; r[1] = 0xe0; r[31] = 0x7a;
            var s = new byte[32]; s[0] = 0x00; s[1] = 0x00; s[2] = 0x9c; s[31] = 0x03;
            var model = new Authorisation7702Signed(new EvmUInt256(7UL),
                "0x000000000000000000000000000000000000dEaD", new EvmUInt256(3UL), r, s, new byte[] { parity });

            var dto = new List<Authorisation7702Signed> { model }.ToRPCAuthorisation()[0];
            var back = dto.ToAuthorisation7702Signed();

            Assert.Equal(r.ToRpcSignatureQuantity(), back.R.ToRpcSignatureQuantity());
            Assert.Equal(s.ToRpcSignatureQuantity(), back.S.ToRpcSignatureQuantity());
            Assert.Equal(new byte[] { parity }.ToRpcSignatureQuantity(), back.V.ToRpcSignatureQuantity());
        }

        [Fact]
        public void ToRPCAuthorisation_RealSignedAuth_RecoversSameSignerAfterRoundTrip()
        {
            var key = EthECKey.GenerateKey();
            var expected = key.GetPublicAddress().ToLowerInvariant();
            var signer = new Authorisation7702Signer();

            for (var nonce = 0; nonce < 12; nonce++)
            {
                var signed = signer.SignAuthorisation(key, new Authorisation7702
                {
                    ChainId = 1,
                    Address = "0x000000000000000000000000000000000000dEaD",
                    Nonce = nonce
                });

                var dto = new List<Authorisation7702Signed> { signed }.ToRPCAuthorisation()[0];
                var recovered = dto.ToAuthorisation7702Signed().RecoverSignerAddress().ToLowerInvariant();

                Assert.Equal(expected, recovered);
            }
        }

        [Fact]
        public void AuthorisationRLPEncode_PaddedVsStrippedRSV_ProducesIdenticalWire()
        {
            var rPadded = new byte[32]; rPadded[31] = 0x01;
            var sPadded = new byte[32]; sPadded[30] = 0x02; sPadded[31] = 0x9c;
            const string addr = "0x000000000000000000000000000000000000dEaD";

            var padded = new List<Authorisation7702Signed>
            {
                new Authorisation7702Signed(new EvmUInt256(1UL), addr, new EvmUInt256(0UL),
                    rPadded, sPadded, new byte[] { 0x00, 0x01 })
            };
            var stripped = new List<Authorisation7702Signed>
            {
                new Authorisation7702Signed(new EvmUInt256(1UL), addr, new EvmUInt256(0UL),
                    new byte[] { 0x01 }, new byte[] { 0x02, 0x9c }, new byte[] { 0x01 })
            };

            Assert.Equal(
                AuthorisationListRLPEncoderDecoder.Encode(padded).ToHex(),
                AuthorisationListRLPEncoderDecoder.Encode(stripped).ToHex());
        }
    }
}
