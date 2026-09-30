using System.IO;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    public class HeaderCodecParityTests
    {
        [Theory]
        [InlineData("block-49439.json")]
        [InlineData("block-51921.json")]
        [InlineData("block-55296.json")]
        [InlineData("block-57257.json")]
        [InlineData("block-62509.json")]
        [InlineData("block-68481.json")]
        [InlineData("block-116525.json")]
        [InlineData("block-314115.json")]
        [InlineData("block-346945.json")]
        [InlineData("block-467857.json")]
        [InlineData("block-505137.json")]
        [InlineData("block-700001.json")]
        [InlineData("block-742497.json")]
        [InlineData("block-1149150.json")]
        [InlineData("block-2180246.json")]
        public void LegacyHeaderCodec_MatchesBlockHeaderEncoder(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);

            var codecBytes = LegacyBlockHeaderCodec.Instance.Encode(header);
            var legacyBytes = BlockHeaderEncoder.Current.Encode(header);

            Assert.Equal(legacyBytes, codecBytes);
        }

        [Theory]
        [InlineData("block-20000000.json")]
        public void CancunHeaderCodec_MatchesBlockHeaderEncoder(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);

            var codecBytes = CancunBlockHeaderCodec.Instance.Encode(header);
            var legacyBytes = BlockHeaderEncoder.Current.Encode(header);

            Assert.Equal(legacyBytes, codecBytes);
        }

        [Theory]
        [InlineData("block-49439.json")]
        [InlineData("block-51921.json")]
        [InlineData("block-2180246.json")]
        public void LegacyHeaderCodec_DecodeRoundTrip_IsByteIdentical(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);

            var encoded = LegacyBlockHeaderCodec.Instance.Encode(header);
            var decoded = LegacyBlockHeaderCodec.Instance.Decode(encoded);
            var reEncoded = LegacyBlockHeaderCodec.Instance.Encode(decoded);

            Assert.Equal(encoded, reEncoded);
        }

        [Theory]
        [InlineData("block-20000000.json")]
        public void CancunHeaderCodec_DecodeRoundTrip_IsByteIdentical(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);

            var encoded = CancunBlockHeaderCodec.Instance.Encode(header);
            var decoded = CancunBlockHeaderCodec.Instance.Decode(encoded);
            var reEncoded = CancunBlockHeaderCodec.Instance.Encode(decoded);

            Assert.Equal(encoded, reEncoded);
        }

        [Fact]
        public void AmsterdamHeaderCodec_DecodeRoundTrip_MatchesFixtureGenesisBlock()
        {
            var path = AmsterdamFixturePath("slotnum_genesis.json");
            Assert.True(File.Exists(path), $"Amsterdam fixture missing at {path}");

            using var document = JsonDocument.Parse(File.ReadAllText(path));
            var test = document.RootElement.EnumerateObject().First().Value;
            var genesisHeader = test.GetProperty("genesisBlockHeader");

            var blockRlp = test.GetProperty("genesisRLP").GetString().HexToByteArray();
            var blockCollection = (RLPCollection)RLP.RLP.Decode(blockRlp);
            var encoded = blockCollection[0].RLPData;

            var expectedHash = genesisHeader.GetProperty("hash").GetString().HexToByteArray();
            var expectedBlockAccessListHash = genesisHeader.GetProperty("blockAccessListHash").GetString().HexToByteArray();
            var expectedSlotNumber = (ulong)new HexBigInteger(genesisHeader.GetProperty("slotNumber").GetString()).Value;

            var decoded = AmsterdamBlockHeaderCodec.Instance.Decode(encoded);

            Assert.Equal(expectedBlockAccessListHash, decoded.BlockAccessListHash);
            Assert.Equal(expectedSlotNumber, decoded.SlotNumber);

            var reEncoded = AmsterdamBlockHeaderCodec.Instance.Encode(decoded);
            Assert.Equal(encoded, reEncoded);
            Assert.Equal(expectedHash, Sha3Keccack.Current.CalculateHash(reEncoded));
        }

        [Theory]
        [InlineData("block-20000000.json")]
        public void AmsterdamHeaderCodec_Decode_RejectsWrongFieldCount(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);
            var cancunEncoded = CancunBlockHeaderCodec.Instance.Encode(header);

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => AmsterdamBlockHeaderCodec.Instance.Decode(cancunEncoded));

            Assert.Equal("Amsterdam header codec expects 23 fields, got 20", ex.Message);
        }

        [Theory]
        [InlineData("block-20000000.json")]
        public void AmsterdamHeaderCodec_Decode_RejectsPragueShapedHeader(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);
            header.RequestsHash = new byte[32];
            var pragueEncoded = PragueBlockHeaderCodec.Instance.Encode(header);

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => AmsterdamBlockHeaderCodec.Instance.Decode(pragueEncoded));

            Assert.Equal("Amsterdam header codec expects 23 fields, got 21", ex.Message);
        }

        [Fact]
        public void PragueHeaderCodec_Decode_RejectsAmsterdamHeader()
        {
            var header = LoadHeader("block-20000000.json");
            header.RequestsHash = new byte[32];
            header.BlockAccessListHash = new byte[32];
            header.SlotNumber = 7;

            var amsterdamEncoded = AmsterdamBlockHeaderCodec.Instance.Encode(header);

            var ex = Assert.Throws<System.InvalidOperationException>(
                () => PragueBlockHeaderCodec.Instance.Decode(amsterdamEncoded));

            Assert.Contains("21", ex.Message);
            Assert.Contains("23", ex.Message);
        }


        private static BlockHeader LoadHeader(string fixtureFile)
        {
            var fixture = LoadFixture(fixtureFile);
            return BuildHeader(fixture.Header);
        }

        private static MainnetBlockFixture LoadFixture(string fileName)
        {
            var path = FixturePath(fileName);
            Assert.True(File.Exists(path), $"Fixture missing at {path}");
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<MainnetBlockFixture>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });
        }

        private static string AmsterdamFixturePath(string fileName)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "external", "execution-spec-tests", "fixtures",
                    "blockchain_tests", "amsterdam", "eip7843_slotnum", "slotnum", fileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "external", "execution-spec-tests", "fixtures",
                "blockchain_tests", "amsterdam", "eip7843_slotnum", "slotnum", fileName);
        }

        private static string FixturePath(string fileName)
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                var candidate = Path.Combine(dir.FullName, "tests", "Nethereum.EVM.UnitTests", "Fixtures", "MainnetBlocks", fileName);
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return Path.Combine(Directory.GetCurrentDirectory(), "Fixtures", "MainnetBlocks", fileName);
        }

        private static BigInteger ParseUnsignedHex(string s)
            => string.IsNullOrEmpty(s) ? BigInteger.Zero : new HexBigInteger(s).Value;

        private static BlockHeader BuildHeader(MainnetBlockHeaderFixture h)
        {
            byte[] HexBytes(string s) => string.IsNullOrEmpty(s) ? null : s.HexToByteArray();
            long ParseLong(string s) => (long)ParseUnsignedHex(s);
            EvmUInt256 ParseU256(string s) => EvmUInt256.FromBigEndian(
                ParseUnsignedHex(s).ToByteArray(isUnsigned: true, isBigEndian: true));

            return new BlockHeader
            {
                ParentHash = HexBytes(h.ParentHash),
                UnclesHash = HexBytes(h.UnclesHash),
                Coinbase = h.Coinbase?.ToLowerInvariant(),
                StateRoot = HexBytes(h.StateRoot),
                TransactionsHash = HexBytes(h.TransactionsRoot),
                ReceiptHash = HexBytes(h.ReceiptsRoot),
                BlockNumber = ParseU256(h.Number),
                LogsBloom = HexBytes(h.LogsBloom) ?? new byte[256],
                Difficulty = ParseU256(h.Difficulty),
                Timestamp = ParseLong(h.Timestamp),
                GasLimit = ParseLong(h.GasLimit),
                GasUsed = ParseLong(h.GasUsed),
                MixHash = HexBytes(h.MixHash) ?? new byte[32],
                ExtraData = HexBytes(h.ExtraData) ?? System.Array.Empty<byte>(),
                Nonce = HexBytes(h.Nonce) ?? new byte[8],
                BaseFee = string.IsNullOrEmpty(h.BaseFee) ? null : (EvmUInt256?)ParseU256(h.BaseFee),
                WithdrawalsRoot = HexBytes(h.WithdrawalsRoot),
                ParentBeaconBlockRoot = HexBytes(h.ParentBeaconBlockRoot)
            };
        }
    }
}
