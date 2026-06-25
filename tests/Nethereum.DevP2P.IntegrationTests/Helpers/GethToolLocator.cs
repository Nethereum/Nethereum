using System;
using System.IO;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// Locates the go-ethereum <c>devp2p</c> binary and its <c>cmd/devp2p</c>
    /// testdata directories on disk. Probes upward from <see cref="AppContext.BaseDirectory"/>
    /// so conformance tests can run from any subdirectory under the repo root.
    /// </summary>
    public static class GethToolLocator
    {
        /// <summary>Relative path of the conformance binary expected under any repo ancestor.</summary>
        public const string DevP2PToolRelativePath = "geth-tools/devp2p.exe";

        /// <summary>Path of the eth-test fixture inside a go-ethereum checkout.</summary>
        public const string EthTestTestdataRelativePath = "go-ethereum/cmd/devp2p/internal/ethtest/testdata";

        /// <summary>Path of the snap-test fixture inside a go-ethereum checkout.</summary>
        public const string SnapTestTestdataRelativePath = "go-ethereum/cmd/devp2p/internal/snaptest/testdata";

        /// <summary>Relative path of the bundled Geth node binary used by Discv4 interop tests.</summary>
        public const string GethBinaryRelativePath = "geth-tools/geth-windows-amd64-1.13.15-c5ba367e/geth.exe";

        /// <summary>Locates <c>geth-tools/devp2p.exe</c> by walking up from the test binary directory.</summary>
        public static string FindDevp2pTool() =>
            ProbeUpFromBaseDirectory(DevP2PToolRelativePath, isDirectory: false)
            ?? throw new FileNotFoundException(
                $"devp2p.exe not found. Expected at <repo-root>/{DevP2PToolRelativePath} — " +
                "see tests/Nethereum.DevP2P.IntegrationTests/README.md for rebuild instructions.");

        /// <summary>Locates the eth-test testdata directory (<c>chain.rlp</c>, <c>genesis.json</c>, etc).</summary>
        public static string FindEthTestTestdata() =>
            ProbeUpFromBaseDirectory(EthTestTestdataRelativePath, isDirectory: true)
            ?? throw new DirectoryNotFoundException(
                $"Geth eth-test testdata not found. Expected at a sibling go-ethereum checkout: <repo-parent>/{EthTestTestdataRelativePath}.");

        /// <summary>Locates the snap-test testdata directory.</summary>
        public static string FindSnapTestTestdata() =>
            ProbeUpFromBaseDirectory(SnapTestTestdataRelativePath, isDirectory: true)
            ?? throw new DirectoryNotFoundException(
                $"Geth snap-test testdata not found. Expected at a sibling go-ethereum checkout: <repo-parent>/{SnapTestTestdataRelativePath}.");

        /// <summary>Locates the bundled <c>geth.exe</c> node binary (separate from <see cref="FindDevp2pTool"/>).</summary>
        public static string FindGethBinary() =>
            ProbeUpFromBaseDirectory(GethBinaryRelativePath, isDirectory: false)
            ?? throw new FileNotFoundException(
                $"Geth 1.13.15 binary not found. Expected at <repo-root>/{GethBinaryRelativePath} - " +
                "this is the full Geth node, distinct from the devp2p conformance tool.");

        private static string ProbeUpFromBaseDirectory(string relativePath, bool isDirectory)
        {
            var probe = AppContext.BaseDirectory;
            while (probe != null)
            {
                var candidate = Path.GetFullPath(Path.Combine(probe, relativePath));
                if (isDirectory ? Directory.Exists(candidate) : File.Exists(candidate))
                    return candidate;
                probe = Path.GetDirectoryName(probe);
            }
            return null;
        }
    }
}
