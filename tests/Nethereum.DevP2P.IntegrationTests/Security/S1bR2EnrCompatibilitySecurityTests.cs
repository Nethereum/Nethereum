using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.IntegrationTests.Security
{
    public class S1bR2EnrCompatibilitySecurityTests
    {
        private static byte[] Str(string s) => RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes(s));

        private static byte[] SignRaw(EthECKey key, byte[] seqItem, params byte[][] keyValueItems)
        {
            var content = new List<byte[]> { seqItem };
            content.AddRange(keyValueItems);
            var digest = new Sha3Keccack().CalculateHash(RLP.RLP.EncodeList(content.ToArray()));
            var sig = key.SignAndCalculateV(digest);
            var sigBytes = new byte[64];
            Buffer.BlockCopy(sig.R.PadBytes(32), 0, sigBytes, 0, 32);
            Buffer.BlockCopy(sig.S.PadBytes(32), 0, sigBytes, 32, 32);
            var record = new List<byte[]> { RLP.RLP.EncodeElement(sigBytes), seqItem };
            record.AddRange(keyValueItems);
            return RLP.RLP.EncodeList(record.ToArray());
        }

        private static byte[][] BasePairs(EthECKey key, params byte[][] extra)
        {
            var pairs = new List<byte[]>
            {
                Str("id"), RLP.RLP.EncodeElement(Encoding.ASCII.GetBytes("v4")),
                Str("ip"), RLP.RLP.EncodeElement(new byte[] { 8, 8, 4, 4 }),
                Str("secp256k1"), RLP.RLP.EncodeElement(key.GetPubKey(true)),
                Str("udp"), RLP.RLP.EncodeElement(new byte[] { 0x76, 0x5f })
            };
            pairs.AddRange(extra);
            return pairs.ToArray();
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWithAnRlpListValueSignedPerEip778_When_Verified_Then_ItIsAccepted()
        {
            var key = EthECKey.GenerateKey();
            var forkId = RLP.RLP.EncodeList(RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[] { 0xfc, 0x64, 0xec, 0x04 }), RLP.RLP.EncodeElement(new byte[] { 0x01 })));
            var pairs = new List<byte[]> { Str("eth"), forkId };
            pairs.AddRange(BasePairs(key));
            var ordered = new List<byte[]> { pairs[0], pairs[1] };
            ordered.AddRange(BasePairs(key));
            var encoded = SignRaw(key, RLP.RLP.EncodeElement(new byte[] { 1 }), Sort(ordered));

            var record = EnrRecordEncoder.Decode(encoded);

            Assert.True(EnrRecordSigner.Verify(record), "a record whose eth value is an RLP list, signed per EIP-778, failed verification");
        }

        [Fact]
        [Trait("Category", "Security")]
        public async System.Threading.Tasks.Task Given_DnsTreeLeafWithAnRlpListEthEntrySignedPerEip778_When_Resolved_Then_TheEnrIsReturned()
        {
            var key = EthECKey.GenerateKey();
            var forkId = RLP.RLP.EncodeList(RLP.RLP.EncodeList(
                RLP.RLP.EncodeElement(new byte[] { 0xfc, 0x64, 0xec, 0x04 }), RLP.RLP.EncodeElement(new byte[] { 0x01 })));
            var pairs = new List<byte[]> { Str("eth"), forkId };
            pairs.AddRange(BasePairs(key));
            var encoded = SignRaw(key, RLP.RLP.EncodeElement(new byte[] { 1 }), Sort(pairs));
            var url = "enr:" + Convert.ToBase64String(encoded).Replace('+', '-').Replace('/', '_').TrimEnd('=');
            var tree = new SignedEnrTree("nodes.example.org");
            tree.SetRoot(tree.AddBranch(tree.Add(url)), tree.AddBranch());
            var resolver = new Nethereum.DevP2P.Dns.EnrTreeResolver(null, SignedEnrTree.Resolver(tree));

            var enrs = await resolver.ResolveEnrsAsync(tree.Url, TimeSpan.FromSeconds(5), 10, System.Threading.CancellationToken.None);

            Assert.Single(enrs);
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWithOnlyByteValuesSignedPerEip778_When_Verified_Then_ItIsAccepted()
        {
            var key = EthECKey.GenerateKey();
            var encoded = SignRaw(key, RLP.RLP.EncodeElement(new byte[] { 1 }), Sort(new List<byte[]>(BasePairs(key))));

            var record = EnrRecordEncoder.Decode(encoded);

            Assert.True(EnrRecordSigner.Verify(record));
        }

        private static byte[][] Sort(List<byte[]> flat)
        {
            var pairs = new List<(string Key, byte[] KeyItem, byte[] ValueItem)>();
            for (var i = 0; i < flat.Count; i += 2)
            {
                var item = flat[i];
                var key = Encoding.ASCII.GetString(item, 1, item.Length - 1);
                pairs.Add((key, item, flat[i + 1]));
            }
            pairs.Sort((a, b) => string.CompareOrdinal(a.Key, b.Key));
            var result = new List<byte[]>();
            foreach (var p in pairs) { result.Add(p.KeyItem); result.Add(p.ValueItem); }
            return result.ToArray();
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWithSequenceEncodedWithALeadingZeroByte_When_Decoded_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var encoded = SignRaw(key, RLP.RLP.EncodeElement(new byte[] { 0x00, 0x01 }), BasePairs(key));

            Assert.ThrowsAny<Exception>(() => EnrRecordEncoder.Decode(encoded));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_RecordWithSingleByteValueEncodedWithALengthPrefix_When_Decoded_Then_ItIsRejected()
        {
            var key = EthECKey.GenerateKey();
            var encoded = SignRaw(key, new byte[] { 0x81, 0x01 }, BasePairs(key));

            Assert.ThrowsAny<Exception>(() => EnrRecordEncoder.Decode(encoded));
        }

        [Fact]
        [Trait("Category", "Security")]
        public void Given_SignedRecordWithMaximumSequence_When_EncodedAndDecoded_Then_SignatureVerifies()
        {
            var key = EthECKey.GenerateKey();
            var enr = new EnrRecord { Sequence = ulong.MaxValue };
            enr.Pairs["ip"] = new byte[] { 8, 8, 4, 4 };
            enr.Pairs["udp"] = new byte[] { 0x76, 0x5f };
            EnrRecordSigner.Sign(enr, key);

            var decoded = EnrRecordEncoder.Decode(EnrRecordEncoder.EncodeRecord(enr));

            Assert.Equal(ulong.MaxValue, decoded.Sequence);
            Assert.True(EnrRecordSigner.Verify(decoded));
        }
    }
}
