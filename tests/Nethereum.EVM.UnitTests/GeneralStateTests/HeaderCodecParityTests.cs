using System.IO;
using System.Numerics;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.EVM.UnitTests.GeneralStateTests
{
    /// <summary>
    /// Per-fork header codec must produce byte-identical output to
    /// <see cref="BlockHeaderEncoder.Current"/> on real mainnet header
    /// fixtures. Catches A4-class silent breakage (null-field substitution
    /// divergence) before any call site migrates from the global encoder
    /// to <c>config.HeaderCodec</c>.
    ///
    /// <para>Fork mapping verified per fixture: Frontier..Berlin → Legacy
    /// (15 fields), London..Shanghai-1 → London (16), Shanghai → Shanghai
    /// (17), Cancun → Cancun (20), Prague onward → Prague (21).</para>
    /// </summary>
    public class HeaderCodecParityTests
    {
        // Frontier-era fixtures: all 15-field, no post-London fields
        // populated. Both legacy encoder and new codec emit 15 fields.
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

        // Cancun-era fixture (block 20M). Has baseFee + withdrawalsRoot +
        // parentBeaconBlockRoot populated. blobGasUsed/excessBlobGas null
        // in fixture; both codec and legacy encoder treat null as 0.
        [Theory]
        [InlineData("block-20000000.json")]
        public void CancunHeaderCodec_MatchesBlockHeaderEncoder(string fixtureFile)
        {
            var header = LoadHeader(fixtureFile);

            var codecBytes = CancunBlockHeaderCodec.Instance.Encode(header);
            var legacyBytes = BlockHeaderEncoder.Current.Encode(header);

            Assert.Equal(legacyBytes, codecBytes);
        }

        // Round-trip property: every codec must satisfy decode(encode(h)) == h
        // for any header it accepts. Validates field index mapping in Decode
        // doesn't drift from Encode.
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

        // -------- Helpers (inlined copy of MainnetBlockReplayTests private
        //          BuildHeader so this test can live as a self-contained
        //          regression cell without changing the access modifier on
        //          the shared helper.) --------

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
