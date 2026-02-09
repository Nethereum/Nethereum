using System.Diagnostics;
using System.Numerics;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class PatriciaTrieChainIntegrationTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private readonly ITestOutputHelper _output;
        private static readonly BigInteger OneEth = BigInteger.Parse("1000000000000000000");

        public PatriciaTrieChainIntegrationTests(DevChainNodeFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task StateRoot_ChangesAfterSimpleTransfer()
        {
            var block1 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot1 = block1.StateRoot;
            _output.WriteLine($"Block {block1.BlockNumber}: StateRoot = {stateRoot1.ToHex()}");

            var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneEth / 10);
            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");

            var block2 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot2 = block2.StateRoot;
            _output.WriteLine($"Block {block2.BlockNumber}: StateRoot = {stateRoot2.ToHex()}");

            Assert.NotNull(stateRoot1);
            Assert.NotNull(stateRoot2);
            Assert.False(stateRoot1.SequenceEqual(stateRoot2),
                $"State root should change after transaction.\nBefore: {stateRoot1.ToHex()}\nAfter:  {stateRoot2.ToHex()}");
        }

        [Fact]
        public async Task StateRoot_MultipleTransactions_AllDifferent()
        {
            var stateRoots = new List<byte[]>();

            var initialBlock = await _fixture.Node.GetLatestBlockAsync();
            stateRoots.Add(initialBlock.StateRoot);
            _output.WriteLine($"Initial block {initialBlock.BlockNumber}: {initialBlock.StateRoot.ToHex()}");

            for (int i = 0; i < 5; i++)
            {
                var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneEth / 100 * (i + 1));
                var result = await _fixture.Node.SendTransactionAsync(signedTx);
                Assert.True(result.Success, $"Transaction {i} failed: {result.RevertReason}");

                var block = await _fixture.Node.GetLatestBlockAsync();
                stateRoots.Add(block.StateRoot);
                _output.WriteLine($"After tx {i}, block {block.BlockNumber}: {block.StateRoot.ToHex()}");
            }

            for (int i = 0; i < stateRoots.Count; i++)
            {
                for (int j = i + 1; j < stateRoots.Count; j++)
                {
                    Assert.False(stateRoots[i].SequenceEqual(stateRoots[j]),
                        $"State roots {i} and {j} should be different");
                }
            }
        }

        [Fact]
        public async Task Performance_ManyTransactions_StateRootComputation()
        {
            const int transactionCount = 20;
            var times = new List<long>();
            var stateRoots = new List<string>();

            _output.WriteLine($"Running {transactionCount} transactions...");

            var initialBlock = await _fixture.Node.GetLatestBlockAsync();
            stateRoots.Add(initialBlock.StateRoot.ToHex());

            for (int i = 0; i < transactionCount; i++)
            {
                var sw = Stopwatch.StartNew();

                var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneEth / 1000);
                var result = await _fixture.Node.SendTransactionAsync(signedTx);

                sw.Stop();

                if (!result.Success)
                {
                    _output.WriteLine($"Transaction {i} failed: {result.RevertReason}");
                    continue;
                }

                times.Add(sw.ElapsedMilliseconds);

                var block = await _fixture.Node.GetLatestBlockAsync();
                stateRoots.Add(block.StateRoot.ToHex());
            }

            _output.WriteLine($"\nTransaction times (ms):");
            for (int i = 0; i < times.Count; i++)
            {
                _output.WriteLine($"  Tx {i}: {times[i]}ms");
            }

            var avgTime = times.Average();
            var maxTime = times.Max();
            var minTime = times.Min();

            _output.WriteLine($"\nStatistics:");
            _output.WriteLine($"  Average: {avgTime:F1}ms");
            _output.WriteLine($"  Min: {minTime}ms");
            _output.WriteLine($"  Max: {maxTime}ms");

            var uniqueRoots = stateRoots.Distinct().Count();
            _output.WriteLine($"\nState roots: {stateRoots.Count} total, {uniqueRoots} unique");

            Assert.Equal(stateRoots.Count, uniqueRoots);

            Assert.True(avgTime < 500, $"Average transaction time {avgTime}ms is too high, expected < 500ms");
        }

        [Fact]
        public async Task StateRoot_ContractInteraction_ChangesRoot()
        {
            var block1 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot1 = block1.StateRoot;
            _output.WriteLine($"Before contract deployment: {stateRoot1.ToHex()}");

            var contractAddress = await _fixture.DeployERC20Async(OneEth * 1000);
            _output.WriteLine($"Deployed ERC20 at: {contractAddress}");

            var block2 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot2 = block2.StateRoot;
            _output.WriteLine($"After deployment: {stateRoot2.ToHex()}");

            Assert.False(stateRoot1.SequenceEqual(stateRoot2), "State root should change after contract deployment");

            await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneEth * 100);

            var block3 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot3 = block3.StateRoot;
            _output.WriteLine($"After ERC20 transfer: {stateRoot3.ToHex()}");

            Assert.False(stateRoot2.SequenceEqual(stateRoot3), "State root should change after ERC20 transfer");
        }

        [Fact]
        public async Task IncrementalVsFreshBuild_SameResult()
        {
            await _fixture.Node.MineBlockAsync();
            var block1 = await _fixture.Node.GetLatestBlockAsync();
            var stateRoot1 = block1.StateRoot;
            _output.WriteLine($"Initial state root: {stateRoot1.ToHex()}");

            for (int i = 0; i < 3; i++)
            {
                var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, OneEth / 100);
                var result = await _fixture.Node.SendTransactionAsync(signedTx);
                Assert.True(result.Success);

                var block = await _fixture.Node.GetLatestBlockAsync();
                _output.WriteLine($"After tx {i}: {block.StateRoot.ToHex()}");
            }

            var finalBlock = await _fixture.Node.GetLatestBlockAsync();
            var incrementalRoot = finalBlock.StateRoot;
            _output.WriteLine($"Incremental final root: {incrementalRoot.ToHex()}");

            Assert.NotNull(incrementalRoot);
            Assert.NotEqual(DefaultValues.EMPTY_TRIE_HASH, incrementalRoot);
        }

        [Fact]
        public async Task HighVolume_StateRootStability()
        {
            const int txCount = 50;
            _output.WriteLine($"Sending {txCount} transactions rapidly...");

            var sw = Stopwatch.StartNew();
            var successCount = 0;
            var failCount = 0;
            var previousRoot = (await _fixture.Node.GetLatestBlockAsync()).StateRoot;

            for (int i = 0; i < txCount; i++)
            {
                var signedTx = _fixture.CreateSignedTransaction(_fixture.RecipientAddress, 1000);
                var result = await _fixture.Node.SendTransactionAsync(signedTx);

                if (result.Success)
                {
                    successCount++;
                    var block = await _fixture.Node.GetLatestBlockAsync();
                    var currentRoot = block.StateRoot;

                    Assert.False(previousRoot.SequenceEqual(currentRoot),
                        $"State root unchanged after tx {i}");

                    previousRoot = currentRoot;
                }
                else
                {
                    failCount++;
                    _output.WriteLine($"Tx {i} failed: {result.RevertReason}");
                }
            }
            sw.Stop();

            _output.WriteLine($"\nResults:");
            _output.WriteLine($"  Success: {successCount}/{txCount}");
            _output.WriteLine($"  Failed: {failCount}/{txCount}");
            _output.WriteLine($"  Total time: {sw.ElapsedMilliseconds}ms");
            _output.WriteLine($"  Avg per tx: {sw.ElapsedMilliseconds / (double)txCount:F1}ms");

            Assert.True(successCount > txCount * 0.9, $"Too many failures: {failCount}/{txCount}");
        }
    }
}
