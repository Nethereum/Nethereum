using System.IO;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    /// <summary>
    /// ethereum/execution-spec-tests osaka/eip7918_blob_reserve_price fixtures (fusaka-devnet-5@v2.1.0),
    /// gating BlockExecutor's calc_excess_blob_gas(parent) reserve-price clause (EIP-7918) the same way
    /// <see cref="Eip4844ExcessBlobGasHostBlockchainTests"/> gates the plain EIP-4844 formula.
    /// </summary>
    public class Eip7918BlobReservePriceHostBlockchainTests
    {
        private readonly HostBlockchainTestRunner _runner;
        private readonly ITestOutputHelper _output;

        public Eip7918BlobReservePriceHostBlockchainTests(ITestOutputHelper output)
        {
            _output = output;
            _runner = new HostBlockchainTestRunner(output);
        }

        [Fact]
        [Trait("Category", "Eip7918BlobReservePrice")]
        public async Task Blob_base_fee_reserve_price_boundary()
        {
            await RunAsync("blob_base_fee", "Osaka");
        }

        private async Task RunAsync(string testFolder, string network)
        {
            var path = Eip7918BlobReservePriceFixtureCorpus.Category(testFolder);
            if (path == null || !Directory.Exists(path))
            {
                _output.WriteLine($"EEST osaka/eip7918_blob_reserve_price/{testFolder} fixtures not found at: {path}");
                _output.WriteLine("Extract fixtures/blockchain_tests/osaka/eip7918_blob_reserve_price from " +
                                   "https://github.com/ethereum/execution-spec-tests/releases/download/fusaka-devnet-5%40v2.1.0/fixtures_fusaka-devnet-5.tar.gz " +
                                   "into external/execution-spec-tests/fixtures/blockchain_tests/osaka/eip7918_blob_reserve_price " +
                                   "(see external/README.md).");
                Assert.True(false, "EEST eip7918_blob_reserve_price fixtures not found - cannot verify the reserve-price clause without them.");
                return;
            }

            await _runner.RunCategoryAsync(path, network);
        }
    }
}
