using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    public class HarnessGethChainImportTests
    {
        [Fact]
        public async Task TheHarnessNodeImportsTheGethGeneratedHiveRpcCompatChainInFull()
        {
            var root = Path.Combine(Path.GetDirectoryName(typeof(HarnessGethChainImportTests).Assembly.Location)!, "Fixtures", "hive-rpc-compat");
            var dataDir = Path.Combine(Path.GetTempPath(), "hive-geth-chain-" + System.Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(dataDir);
            var options = new Nethereum.Node.HarnessServer.HarnessServerOptions
            {
                DataDir = dataDir,
                HttpPort = 18580,
                AuthRpcPort = 18581,
                GenesisPath = Path.Combine(root, "genesis.json"),
                ImportChainRlpPath = Path.Combine(root, "chain.rlp"),
                ImportBlocksDir = Path.Combine(dataDir, "blocks"),
            };
            var app = await Nethereum.Node.HarnessServer.HarnessNodeHost.BuildAsync(options, System.Array.Empty<string>());
            try
            {
                var node = Microsoft.Extensions.DependencyInjection.ServiceProviderServiceExtensions
                    .GetRequiredService<Nethereum.DevChain.DevChainNode>(app.Services);
                var head = await node.GetBlockNumberAsync();
                Assert.Equal(54, (int)head);
            }
            finally
            {
                await app.DisposeAsync();
                try { Directory.Delete(dataDir, true); } catch { }
            }
        }
    }
}
