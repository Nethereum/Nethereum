using System.IO;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.EVM.Core.Tests.GeneralStateTests
{
    public class BlockchainSyncTests
    {
        private readonly ITestOutputHelper _output;
        private readonly BlockchainSyncTestRunner _runner;
        private readonly string _testVectorsPath;

        public BlockchainSyncTests(ITestOutputHelper output)
        {
            _output = output;
            _runner = new BlockchainSyncTestRunner(output);
            _testVectorsPath = GetBlockchainTestsPath();
        }

        // === Cancun fork tests ===

        [Fact]
        public void ValidBlocks_bcExample()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcExample");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcValidBlockTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcValidBlockTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcStateTests()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcStateTests");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcEIP1559()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcEIP1559");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcBlockGasLimitTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcBlockGasLimitTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcGasPricerTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcGasPricerTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcWalletTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcWalletTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcEIP1153()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcEIP1153-transientStorage");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcEIP3675()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcEIP3675");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcExploitTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcExploitTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcForkStressTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcForkStressTest");
            _runner.RunCategory(path);
        }

        [Fact]
        public void ValidBlocks_bcRandomBlockhashTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcRandomBlockhashTest");
            _runner.RunCategory(path);
        }

        // === Prague fork tests ===

        [Fact]
        public void Prague_bcExample()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcExample");
            _runner.RunCategory(path, "Prague");
        }

        [Fact]
        public void Prague_bcValidBlockTest()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcValidBlockTest");
            _runner.RunCategory(path, "Prague");
        }

        [Fact]
        public void Prague_bcStateTests()
        {
            var path = Path.Combine(_testVectorsPath, "ValidBlocks", "bcStateTests");
            _runner.RunCategory(path, "Prague");
        }

        private static string GetBlockchainTestsPath()
        {
            var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
            while (dir != null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Nethereum.slnx")) ||
                    File.Exists(Path.Combine(dir.FullName, "Nethereum.sln")))
                {
                    var path = Path.Combine(dir.FullName, "external", "ethereum-tests", "BlockchainTests");
                    if (Directory.Exists(path)) return path;
                }
                dir = dir.Parent;
            }
            return null;
        }
    }
}
