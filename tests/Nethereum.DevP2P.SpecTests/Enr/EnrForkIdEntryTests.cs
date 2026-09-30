using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Model.P2P;
using Nethereum.RLP;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Xunit;
using Nethereum.EVM.ForkId;

namespace Nethereum.DevP2P.SpecTests.Enr
{
    public class EnrForkIdEntryTests
    {
        [Fact]
        public void EnrForkIdEntry_RoundTrips_HashAndNext()
        {
            var encoded = EnrForkIdEntry.Encode(0xdeadbeef, 15_050_000UL);

            EnrForkIdEntry.Decode(encoded, out var forkHash, out var forkNext);

            Assert.Equal(0xdeadbeefu, forkHash);
            Assert.Equal(15_050_000UL, forkNext);
        }

        [Fact]
        public void EnrForkIdEntry_RoundTrips_HashAndNext_MutatedNextDecodesDifferently()
        {
            var original = EnrForkIdEntry.Encode(0xdeadbeef, 15_050_000UL);
            var mutated = EnrForkIdEntry.Encode(0xdeadbeef, 15_050_001UL);

            EnrForkIdEntry.Decode(original, out _, out var originalNext);
            EnrForkIdEntry.Decode(mutated, out _, out var mutatedNext);

            Assert.NotEqual(originalNext, mutatedNext);
            Assert.NotEqual(original.ToHex(), mutated.ToHex());
        }

        [Fact]
        public void EnrForkIdEntry_ProducesGethNestedListShape()
        {
            var encoded = EnrForkIdEntry.Encode(0xfc64ec04, 1_150_000UL);

            var outer = (RLPCollection)RLP.RLP.Decode(encoded);
            Assert.IsType<RLPCollection>(outer[0]);

            var forkIdPair = (RLPCollection)outer[0];
            Assert.Equal(2, forkIdPair.Count);
        }

        [Fact]
        public void EnrForkIdEntry_Decode_ToleratesTail()
        {
            var forkId = ForkIdEncoder.Encode(0xfc64ec04, 1_150_000UL);
            var withTail = RLP.RLP.EncodeList(forkId, RLP.RLP.EncodeElement(new byte[] { 0x01 }));

            EnrForkIdEntry.Decode(withTail, out var forkHash, out var forkNext);

            Assert.Equal(0xfc64ec04u, forkHash);
            Assert.Equal(1_150_000UL, forkNext);
        }

        [Fact]
        public void EnrForkIdEntry_Encode_IsNotFlatForkIdPair()
        {
            var flat = ForkIdEncoder.Encode(0xfc64ec04, 1_150_000UL);
            var nested = EnrForkIdEntry.Encode(0xfc64ec04, 1_150_000UL);

            Assert.NotEqual(flat.ToHex(), nested.ToHex());
        }

        [Fact]
        public void LocalEnr_IncludesEthForkIdEntry()
        {
            var genesisHash = new byte[32];
            genesisHash[0] = 0xab;
            var headBlock = 15_100_000UL;
            var headTime = 1_700_000_000UL;

            var expected = Eip2124ForkIdCalculator.NewId(
                genesisHash,
                new ulong[] { 1_150_000UL, 15_050_000UL },
                new ulong[] { 1_681_338_455UL },
                headBlock, headTime);

            var key = EthECKey.GenerateKey();
            var localEnr = new EnrRecord { Sequence = 1 };
            localEnr.Pairs["id"] = System.Text.Encoding.ASCII.GetBytes("v4");
            localEnr.Pairs["eth"] = EnrForkIdEntry.Encode(expected.Hash, expected.Next);
            EnrRecordSigner.Sign(localEnr, key);

            var wireEncoded = EnrRecordEncoder.EncodeRecord(localEnr);
            var decodedEnr = EnrRecordEncoder.Decode(wireEncoded);

            Assert.True(decodedEnr.Pairs.TryGetValue("eth", out var ethBytes));
            EnrForkIdEntry.Decode(ethBytes, out var actualHash, out var actualNext);
            Assert.Equal(expected.Hash, actualHash);
            Assert.Equal(expected.Next, actualNext);
        }
    }
}
