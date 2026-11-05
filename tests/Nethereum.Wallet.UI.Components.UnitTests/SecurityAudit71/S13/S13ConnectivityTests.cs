using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Nethereum.WalletConnect;
using Xunit;

namespace Nethereum.Wallet.UI.Components.UnitTests.SecurityAudit71.S13
{
    public class S13ConnectivityTests
    {
        private static string RepoFile(string relative)
        {
            var directory = new DirectoryInfo(AppContext.BaseDirectory);
            while (directory != null && !File.Exists(Path.Combine(directory.FullName, "Nethereum.slnx")))
            {
                directory = directory.Parent;
            }
            return Path.Combine(directory!.FullName, relative);
        }

        [Fact]
        public void Given_ADappThatAsksForPolygonOnly_When_TheConnectOptionsAreBuilt_Then_MainnetIsNotARequiredChain()
        {
            var options = NethereumWalletConnectService.GetDefaultConnectOptions(new long[] { 137 });

            var required = options.RequiredNamespaces["eip155"].Chains;

            Assert.DoesNotContain("eip155:1", required);
        }

        [Fact]
        public async Task Given_ADappThatAsksForPolygonOnly_When_TheConnectionIsInitialised_Then_TheSelectedChainIsPolygon()
        {
            var service = new NethereumWalletConnectService(null!);
            var options = NethereumWalletConnectService.GetDefaultConnectOptions(new long[] { 137 });

            await Assert.ThrowsAnyAsync<Exception>(() => service.InitialiseConnectionAndGetQRUriAsync(options));

            Assert.Equal("eip155:137", service.SelectedChainId);
        }

        [Fact]
        public void Given_TheUnityEip6963Bridge_When_ItsInteropMembersAreCalled_Then_EachOneIsDefined()
        {
            var jslib = File.ReadAllText(RepoFile(Path.Combine("src", "Nethereum.Unity.EIP6963", "NethereumEIP6963.jslib")));
            var called = Regex.Matches(jslib, @"window\.NethereumEIP6963Interop\.(\w+)\(").Select(m => m.Groups[1].Value).Distinct();
            var defined = Regex.Matches(jslib, @"(\w+)\s*:\s*(async\s+)?function").Select(m => m.Groups[1].Value).ToHashSet();

            var missing = called.Where(name => !defined.Contains(name)).ToList();

            Assert.Empty(missing);
        }
    }
}
