using System;
using System.IO;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class AmsterdamHeaderAttributionTests
    {
        private readonly ITestOutputHelper _output;

        public AmsterdamHeaderAttributionTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        [Trait("Category", "AmsterdamHeader")]
        public void Given_AFixtureHeaderWithAllTwentyThreeFields_When_EncodedByTheAmsterdamCodec_Then_ItHashesToTheFixturesOwnHash()
        {
            var fixtures = AmsterdamFixtureHeaders.Load(_output);
            Assert.True(fixtures.Count > 0,
                "No Amsterdam fixture headers with a blockAccessListHash were found. " +
                "This test cannot report a pass without them — see AmsterdamFixtureHeaders for the search path.");

            var matched = 0;
            var mismatched = 0;
            foreach (var f in fixtures)
            {
                var encoded = AmsterdamBlockHeaderCodec.Instance.Encode(f.Header);
                var hash = new Sha3Keccack().CalculateHash(encoded);

                if (hash.SequenceEqual(f.ExpectedHash))
                {
                    matched++;
                }
                else
                {
                    if (mismatched < 5)
                        _output.WriteLine($"  MISMATCH {f.Source}: expected=0x{f.ExpectedHash.ToHex()} actual=0x{hash.ToHex()}");
                    mismatched++;
                }
            }

            _output.WriteLine($"Amsterdam headers: {matched} reproduce their own hash, {mismatched} do not (of {fixtures.Count}).");

            Assert.True(mismatched == 0,
                $"{mismatched} of {fixtures.Count} Amsterdam fixture headers do not hash to the value the fixture " +
                "states when encoded through the 23-field codec. The field layout is therefore wrong, and the " +
                "block-hash failures are NOT solely the encoder-selection gap they have been attributed to.");
        }

        [Fact]
        [Trait("Category", "AmsterdamHeader")]
        public void Given_CompleteAmsterdamHeaders_When_EncodedByEitherEncoder_Then_TheBytesAreIdentical()
        {
            var fixtures = AmsterdamFixtureHeaders.Load(_output);
            Assert.True(fixtures.Count > 0, "No Amsterdam fixture headers found.");

            var disagreed = 0;
            foreach (var f in fixtures)
            {
                var viaCodec = AmsterdamBlockHeaderCodec.Instance.Encode(f.Header);
                var viaCascade = BlockHeaderEncoder.Current.Encode(f.Header);
                if (!viaCodec.SequenceEqual(viaCascade))
                {
                    if (disagreed < 3)
                        _output.WriteLine($"  DISAGREE {f.Source}: codec={viaCodec.Length}B cascade={viaCascade.Length}B");
                    disagreed++;
                }
            }

            _output.WriteLine($"Codec and cascade agree on {fixtures.Count - disagreed} of {fixtures.Count} complete Amsterdam headers.");
            Assert.True(disagreed == 0,
                $"{disagreed} headers encode differently through the two encoders that both claim to describe the " +
                "23-field Amsterdam layout.");
        }

        [Fact]
        [Trait("Category", "AmsterdamHeader")]
        public void Given_TheHeaderShapeTheEngineBuildsToday_When_Hashed_Then_ItLosesTheSlotNumberToo()
        {
            var fixtures = AmsterdamFixtureHeaders.Load(_output);
            Assert.True(fixtures.Count > 0, "No Amsterdam fixture headers found.");

            var sample = fixtures[0];

            var asEngineBuildsIt = Clone(sample.Header);
            asEngineBuildsIt.BlockAccessListHash = null;

            var encoded = BlockHeaderEncoder.Current.Encode(asEngineBuildsIt);
            var hash = new Sha3Keccack().CalculateHash(encoded);
            var fieldCount = Nethereum.RLP.RLP.Decode(encoded) is RLPCollection c ? c.Count : -1;

            _output.WriteLine($"With BlockAccessListHash absent: {fieldCount} fields encoded (complete header is 23).");
            _output.WriteLine($"  expected=0x{sample.ExpectedHash.ToHex()}");
            _output.WriteLine($"  actual  =0x{hash.ToHex()}");

            Assert.Equal(21, fieldCount);
            Assert.False(hash.SequenceEqual(sample.ExpectedHash),
                "A header missing its block access list hash reproduced the fixture hash. Then the field is not " +
                "part of the commitment and the attribution of these failures is wrong.");
            Assert.NotNull(sample.Header.SlotNumber);
        }

        private static BlockHeader Clone(BlockHeader h)
        {
            return new BlockHeader
            {
                ParentHash = h.ParentHash,
                UnclesHash = h.UnclesHash,
                Coinbase = h.Coinbase,
                StateRoot = h.StateRoot,
                TransactionsHash = h.TransactionsHash,
                ReceiptHash = h.ReceiptHash,
                LogsBloom = h.LogsBloom,
                Difficulty = h.Difficulty,
                BlockNumber = h.BlockNumber,
                GasLimit = h.GasLimit,
                GasUsed = h.GasUsed,
                Timestamp = h.Timestamp,
                ExtraData = h.ExtraData,
                MixHash = h.MixHash,
                Nonce = h.Nonce,
                BaseFee = h.BaseFee,
                WithdrawalsRoot = h.WithdrawalsRoot,
                BlobGasUsed = h.BlobGasUsed,
                ExcessBlobGas = h.ExcessBlobGas,
                ParentBeaconBlockRoot = h.ParentBeaconBlockRoot,
                RequestsHash = h.RequestsHash,
                BlockAccessListHash = h.BlockAccessListHash,
                SlotNumber = h.SlotNumber
            };
        }
    }
}
