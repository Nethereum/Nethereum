using System.IO;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Canary test: build the canonical Ethereum mainnet genesis state from
    /// <c>testdata/mainnet/genesis.json</c> (8,893 alloc entries dumped from
    /// go-ethereum's <c>core.DefaultGenesisBlock()</c>) and assert the computed
    /// state root matches <see cref="MainnetGenesisConstants.StateRootHex"/>.
    /// <para>
    /// This is the smallest milestone that proves the Nethereum.CoreChain
    /// state-trie machinery works on real mainnet data — no chain.rlp or
    /// block replay is required. Once green, the next milestone is replaying
    /// blocks 1..N against canonical state roots.
    /// </para>
    /// </summary>
    public class MainnetGenesisCanaryTests
    {
        private readonly ITestOutputHelper _output;
        public MainnetGenesisCanaryTests(ITestOutputHelper output) { _output = output; }

        [Fact]
        public void BuildMainnetGenesis_StateRootMatchesCanonical()
        {
            var fixturePath = LocateMainnetGenesisFixture();
            _output.WriteLine($"Loading mainnet genesis from {fixturePath}");

            var header = GethTestdataGenesisBuilder.Build(fixturePath);

            _output.WriteLine($"Computed stateRoot: 0x{header.StateRoot.ToHex()}");
            _output.WriteLine($"Computed gasLimit:  {header.GasLimit}");
            _output.WriteLine($"Computed coinbase:  {header.Coinbase}");

            Assert.Equal(
                MainnetGenesisConstants.StateRootHex,
                ("0x" + header.StateRoot.ToHex()).ToLowerInvariant());
            Assert.Equal(MainnetGenesisConstants.GasLimit, header.GasLimit);
        }

        private static string LocateMainnetGenesisFixture()
        {
            var probe = System.AppContext.BaseDirectory;
            while (probe != null)
            {
                var candidate = Path.Combine(probe, "testdata", "mainnet", "genesis.json");
                if (File.Exists(candidate)) return Path.GetFullPath(candidate);
                probe = Path.GetDirectoryName(probe);
            }
            throw new FileNotFoundException(
                "Mainnet genesis fixture not found. Expected at " +
                "<test-binary-dir-ancestor>/testdata/mainnet/genesis.json.");
        }
    }
}
