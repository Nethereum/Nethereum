using System.IO;
using Nethereum.Testing;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public static class GethToolLocator
    {
        private const string EthTestTestdataSubPath = "cmd/devp2p/internal/ethtest/testdata";

        private const string SnapTestTestdataSubPath = "cmd/devp2p/internal/snaptest/testdata";

        public static string FindDevp2pTool() => RequireTool("Devp2p", "the devp2p conformance tool");

        public static string FindEthTestTestdata() => RequireGoEthereumDirectory(EthTestTestdataSubPath);

        public static string FindSnapTestTestdata() => RequireGoEthereumDirectory(SnapTestTestdataSubPath);

        public static string FindGethBinary() => RequireTool("Geth1_13", "the full geth 1.13.15 node");

        private static string RequireTool(string key, string description)
        {
            var path = TestEnvironment.Tool(key);
            if (!File.Exists(path))
                throw new FileNotFoundException(
                    $"Tools:{key} ({description}) not found at '{path}'. " +
                    $"Run dotnet run ci/fetch-externals.cs, or set Tools:{key} in {TestEnvironment.LocalSettingsRelativePath}.");
            return path;
        }

        private static string RequireGoEthereumDirectory(string subPath)
        {
            var path = Path.Combine(TestEnvironment.RequireExternalPath("GoEthereum"), subPath);
            if (!Directory.Exists(path))
                throw new DirectoryNotFoundException(
                    $"'{subPath}' not found under ExternalPaths:GoEthereum at '{path}'; it must be a go-ethereum source checkout.");
            return path;
        }
    }
}
