using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth
{
    /// <summary>
    /// EIP-2124 fork-ID test vectors. These are the canonical reference values
    /// from go-ethereum's `core/forkid/forkid_test.go` for Mainnet and Sepolia.
    /// If we produce a different ForkID for the same chain state, our peers
    /// would reject our Status messages as "fork ID mismatch".
    ///
    /// Spec: https://eips.ethereum.org/EIPS/eip-2124
    /// Reference impl: https://github.com/ethereum/go-ethereum/blob/master/core/forkid/forkid.go
    /// </summary>
    public class Eip2124ForkIdCalculatorTests
    {
        // Mainnet genesis hash.
        private static readonly byte[] MainnetGenesis =
            "d4e56740f876aef8c010b86a40d5f56745a118d0906a34e69aec8c0db1cb8fa3".HexToByteArray();

        // Mainnet fork blocks per the CURRENT go-ethereum MainnetChainConfig.
        // MergeNetsplitBlock is intentionally NOT in this list — the field
        // exists in ChainConfig but is left nil for mainnet, so gatherForks
        // doesn't emit a 15537394 entry. The transition to PoS at the Merge
        // is captured by the consensus engine, not the EIP-2124 fork chain.
        private static readonly ulong[] MainnetForkBlocks = new ulong[]
        {
            1_150_000UL,  // Homestead
            1_920_000UL,  // DAO fork
            2_463_000UL,  // Tangerine Whistle (EIP-150)
            2_675_000UL,  // Spurious Dragon (EIP-155/158, both at same block)
            4_370_000UL,  // Byzantium
            7_280_000UL,  // Constantinople + Petersburg (same block)
            9_069_000UL,  // Istanbul
            9_200_000UL,  // Muir Glacier
            12_244_000UL, // Berlin
            12_965_000UL, // London
            13_773_000UL, // Arrow Glacier
            15_050_000UL  // Gray Glacier
        };

        private static readonly ulong[] MainnetForkTimestamps = new ulong[]
        {
            1_681_338_455UL, // Shanghai
            1_710_338_135UL  // Cancun
        };

        [Fact]
        public void Mainnet_GenesisOnly_MatchesFcOnly_ForFrontierEra()
        {
            // Reference: forkid_test.go — Frontier era expects 0xfc64ec04.
            // We compute the hash WITH ALL FORKS the local node knows about.
            // To compare to the "Frontier-era" value we'd need to truncate to
            // an empty fork list — that's the bare crc32(genesis) value.
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(MainnetGenesis, new ulong[0], new ulong[0]);
            Assert.Equal(0xfc64ec04u, forkHash);
        }

        [Fact]
        public void Mainnet_AfterHomestead_MatchesGoEthereumReference()
        {
            // Reference: forkid_test.go — Homestead-era fork hash 0x97c2c34c.
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL },
                new ulong[0]);
            Assert.Equal(0x97c2c34cu, forkHash);
        }

        [Fact]
        public void Mainnet_AfterDao_MatchesGoEthereumReference()
        {
            // Reference: forkid_test.go — DAO-era fork hash 0x91d1f948.
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 1_920_000UL },
                new ulong[0]);
            Assert.Equal(0x91d1f948u, forkHash);
        }

        [Fact]
        public void Mainnet_AfterPetersburg_MatchesGoEthereumReference()
        {
            // Reference: forkid_test.go — Petersburg fork hash 0x668db0af.
            // Constantinople + Petersburg share block 7_280_000 → dedup.
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 1_920_000UL, 2_463_000UL, 2_675_000UL, 4_370_000UL, 7_280_000UL, 7_280_000UL },
                new ulong[0]);
            Assert.Equal(0x668db0afu, forkHash);
        }

        [Fact]
        public void Mainnet_AfterShanghai_MatchesGoEthereumReference()
        {
            // Reference: forkid_test.go — Shanghai fork hash 0xdce96c2d.
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                MainnetForkBlocks,
                new ulong[] { 1_681_338_455UL });
            Assert.Equal(0xdce96c2du, forkHash);
        }

        [Fact]
        public void Mainnet_AfterCancun_MatchesGoEthereumReference()
        {
            // Reference: forkid_test.go — Cancun fork hash 0x9f3d2254.
            var forkHash = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                MainnetForkBlocks,
                MainnetForkTimestamps);
            Assert.Equal(0x9f3d2254u, forkHash);
        }

        [Fact]
        public void DuplicateForkBlocks_AreDropped()
        {
            // Constantinople (7280000) and Petersburg (7280000) share a block.
            // Per spec, duplicates must be dropped before hashing.
            var withDups = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 7_280_000UL, 7_280_000UL },
                new ulong[0]);
            var withoutDups = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL, 7_280_000UL },
                new ulong[0]);
            Assert.Equal(withoutDups, withDups);
        }

        [Fact]
        public void ZeroForkValues_AreSkipped()
        {
            // A fork at block 0 is part of genesis and is not included in the
            // hash chain per spec.
            var withZero = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 0UL, 1_150_000UL },
                new ulong[] { 0UL, 1_681_338_455UL });
            var withoutZero = Eip2124ForkIdCalculator.ComputeForkHash(
                MainnetGenesis,
                new ulong[] { 1_150_000UL },
                new ulong[] { 1_681_338_455UL });
            Assert.Equal(withoutZero, withZero);
        }
    }
}
