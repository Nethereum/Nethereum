using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.BlockchainTests
{
    public class Eip658PreByzantiumReceiptPostStateTests
    {
        private readonly HostBlockchainTestRunner _runner;

        public Eip658PreByzantiumReceiptPostStateTests(ITestOutputHelper output)
        {
            _runner = new HostBlockchainTestRunner(output);
        }

        private static BlockchainTestLoader.BlockchainTest LoadCase(string fileName, string forkFragment, string caseFragment)
        {
            var filePath = Path.Combine(FrontierValidationFixtureCorpus.Path, fileName);
            var tests = BlockchainTestLoader.LoadFromFile(filePath);
            return tests.Single(t => t.Name.Contains(forkFragment) && t.Name.Contains(caseFragment));
        }

        [Theory]
        [Trait("Category", "Eip658PreByzantiumReceiptPostState")]
        [InlineData("fork_Frontier")]
        [InlineData("fork_Homestead")]
        public async Task Given_PreByzantiumBlock_When_ReceiptConstructed_Then_Field0IsIntermediateStateRoot(string forkFragment)
        {
            var test = LoadCase("test_sender_balance.json", forkFragment, "balance_diff_0-expected_exception_None");

            var result = await _runner.RunSingleBlockTestAsync(test);

            Assert.True(result.RootMatches);
            var receipt = Assert.Single(result.ExecutionResults).Receipt;
            Assert.NotNull(receipt.PostStateOrStatus);
            Assert.Equal(32, receipt.PostStateOrStatus.Length);
        }

        [Theory]
        [Trait("Category", "Eip658PreByzantiumReceiptPostState")]
        [InlineData("fork_Frontier")]
        [InlineData("fork_Homestead")]
        public async Task Given_PreByzantiumBlock_When_ReceiptConstructed_Then_Field0IsNotStatusByte(string forkFragment)
        {
            var test = LoadCase("test_tx_nonce.json", forkFragment, "nonce_diff_0-expected_exception_None");

            var result = await _runner.RunSingleBlockTestAsync(test);

            var receipt = Assert.Single(result.ExecutionResults).Receipt;
            Assert.False(receipt.IsStatusReceipt);
            Assert.True(receipt.PostStateOrStatus.Length > 1);
        }

        [Theory]
        [Trait("Category", "Eip658PreByzantiumReceiptPostState")]
        [InlineData("fork_Byzantium")]
        [InlineData("fork_Shanghai")]
        public async Task Given_ByzantiumPlusBlock_When_ReceiptConstructed_Then_Field0IsStatusCode(string forkFragment)
        {
            var test = LoadCase("test_sender_balance.json", forkFragment, "balance_diff_0-expected_exception_None");

            var result = await _runner.RunSingleBlockTestAsync(test);

            Assert.True(result.RootMatches);
            var receipt = Assert.Single(result.ExecutionResults).Receipt;
            Assert.True(receipt.IsStatusReceipt);
            Assert.True(receipt.PostStateOrStatus.Length <= 1);
        }
    }
}
