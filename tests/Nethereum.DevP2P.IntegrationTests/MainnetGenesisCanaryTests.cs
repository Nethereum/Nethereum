using System.IO;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.DevP2P.IntegrationTests
{
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
