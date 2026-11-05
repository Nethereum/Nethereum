using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Numerics;
using System.Text;
using Nethereum.DevP2P.Discv5;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class EnrRecordSecurityTests
    {
        private static readonly BigInteger Secp256k1Order = BigInteger.Parse(
            "0FFFFFFFFFFFFFFFFFFFFFFFFFFFFFFFEBAAEDCE6AF48A03BBFD25E8CD0364141", System.Globalization.NumberStyles.HexNumber);

        private static EnrRecord SignedRecord(EthECKey key, Action<EnrRecord> customise = null)
        {
            var enr = new EnrRecord { Sequence = 1 };
            enr.Pairs["ip"] = new byte[] { 8, 8, 4, 4 };
            enr.Pairs["tcp"] = new byte[] { 0x76, 0x5f };
            enr.Pairs["udp"] = new byte[] { 0x76, 0x5f };
            customise?.Invoke(enr);
            EnrRecordSigner.Sign(enr, key);
            return enr;
        }

        private static byte[] EncodeUnchecked(EnrRecord enr, IEnumerable<KeyValuePair<string, byte[]>> orderedPairs)
        {
            var elements = new List<byte[]>
            {
                RLP.RLP.EncodeElement(enr.Signature),
                RLP.RLP.EncodeElement(new byte[] { (byte)enr.Sequence })
            };
            foreach (var kv in orderedPairs)
            {
                elements.Add(RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(kv.Key)));
                elements.Add(RLP.RLP.EncodeElement(kv.Value));
            }
            return RLP.RLP.EncodeList(elements.ToArray());
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_ValidSignedRecord_When_EncodedAndDecoded_Then_SignatureVerifies()
        {
            var enr = SignedRecord(EthECKey.GenerateKey());

            var decoded = EnrRecordEncoder.Decode(EnrRecordEncoder.EncodeRecord(enr));

            Assert.True(EnrRecordSigner.Verify(decoded));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_SignedRecordLargerThan300Bytes_When_Decoded_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var enr = SignedRecord(key, e => e.Pairs["junk"] = new byte[400]);
            var encoded = EncodeUnchecked(enr, enr.Pairs);
            Assert.True(encoded.Length > EnrRecord.MaxEncodedSize);
            Assert.True(EnrRecordSigner.Verify(enr));

            Assert.ThrowsAny<Exception>(() => EnrRecordEncoder.Decode(encoded));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWhoseSignatureHasTheHighSValue_When_Verified_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var enr = SignedRecord(key);
            var s = new BigInteger(enr.Signature.Skip(32).Take(32).Reverse().Concat(new byte[] { 0 }).ToArray());
            var flipped = (Secp256k1Order - s).ToByteArray().Reverse().SkipWhile(b => b == 0).ToArray();
            var padded = new byte[32];
            Buffer.BlockCopy(flipped, 0, padded, 32 - flipped.Length, flipped.Length);
            var malleated = new EnrRecord { Sequence = enr.Sequence, Signature = enr.Signature.Take(32).Concat(padded).ToArray() };
            foreach (var kv in enr.Pairs) malleated.Pairs[kv.Key] = kv.Value;

            Assert.False(EnrRecordSigner.Verify(malleated),
                "a second, high-S encoding of the same record verifies (geth enode/idscheme.go uses crypto.VerifySignature, which rejects non-lower-half S)");
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWithKeysOutOfOrder_When_Decoded_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var enr = SignedRecord(key);
            var reversed = enr.Pairs.Reverse().ToList();
            var encoded = EncodeUnchecked(enr, reversed);

            Assert.ThrowsAny<Exception>(() => EnrRecordEncoder.Decode(encoded));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWithDuplicateKey_When_Decoded_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var enr = SignedRecord(key);
            var withDuplicate = enr.Pairs.ToList();
            withDuplicate.Insert(1, new KeyValuePair<string, byte[]>(withDuplicate[1].Key, withDuplicate[1].Value));
            var encoded = EncodeUnchecked(enr, withDuplicate);

            Assert.ThrowsAny<Exception>(() => EnrRecordEncoder.Decode(encoded));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_PortValueLongerThanTwoBytes_When_ReadFromRecord_Then_ItIsNotTruncatedIntoAPort()
        {
            var enr = new EnrRecord();
            enr.Pairs["udp"] = new byte[] { 1, 2, 3 };
            enr.Pairs["tcp"] = new byte[] { 0, 80 };

            Assert.Null(enr.UdpPort);
            Assert.Null(enr.TcpPort);
        }

        [Theory]
        [Trait("Category", "Security")]
        [InlineData("224.0.0.251")]
        [InlineData("255.255.255.255")]
        [InlineData("0.1.2.3")]
        public void Given_SignedRecordWithSpecialNetworkIp_When_ConvertedToAnEnode_Then_NoDialTargetIsProduced(string ip)
        {
            var enr = SignedRecord(EthECKey.GenerateKey(), e => e.Pairs["ip"] = IPAddress.Parse(ip).GetAddressBytes());

            Assert.Null(Discv5PeerDiscoveryService.ConvertEnrToEnode(enr));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_SignedRecordWithPublicIp_When_ConvertedToAnEnode_Then_DialTargetIsProduced()
        {
            var enr = SignedRecord(EthECKey.GenerateKey());

            Assert.NotNull(Discv5PeerDiscoveryService.ConvertEnrToEnode(enr));
        }
    }
}
