using System.Numerics;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AppChain.IntegrationTests.SecurityAudit71.S4.Round3
{
    public class BlockProducerFaultIsolationTests
    {
        private const string UnwiredKzgPrecompile = "0x000000000000000000000000000000000000000a";
        private const string Honest = "0x0000000000000000000000000000000000000001";
        private const long Gwei = 1_000_000_000;
        private readonly ITestOutputHelper _out;

        public BlockProducerFaultIsolationTests(ITestOutputHelper output) { _out = output; }

        private static ISignedTransaction Signed(Nethereum.Web3.Accounts.Account account, BigInteger chainId, string to, long nonce = 0)
        {
            var hex = new LegacyTransactionSigner().SignTransaction(
                account.PrivateKey.HexToByteArray(), chainId, to, BigInteger.Zero, nonce, 10 * Gwei, 500_000, "00");
            return TransactionFactory.CreateTransaction(hex.EnsureHexPrefix());
        }

        private static async Task<(DevChainNode Node, Nethereum.Web3.Accounts.Account[] Accounts)> StartAsync(int accounts)
        {
            var node = new DevChainNode();
            await node.StartAsync();
            return (node, await node.GenerateAndFundAccountsAsync(accounts));
        }

        private static string[] Hashes(IEnumerable<ISignedTransaction> transactions) =>
            transactions.Select(t => t.Hash.ToHex()).ToArray();

        [Fact]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_ATransactionInTheMiddleFaultsTheHost_When_ABlockIsMined_Then_TheOnesBeforeAndAfterItAreIncludedInTheSameBlockAndOnlyItIsDropped()
        {
            var (node, accounts) = await StartAsync(3);
            var chainId = node.DevConfig.ChainId;
            var before = Signed(accounts[0], chainId, Honest);
            var poison = Signed(accounts[1], chainId, UnwiredKzgPrecompile);
            var after = Signed(accounts[2], chainId, Honest);
            node.BlockManager.AddPendingTransaction(before);
            node.BlockManager.AddPendingTransaction(poison);
            node.BlockManager.AddPendingTransaction(after);

            await node.BlockManager.MineBlockAsync();
            var block = node.BlockManager.LastBlockProductionResult;

            Assert.Equal(Hashes(new[] { before, after }), Hashes(block.IncludedTransactions));
            Assert.Equal(Hashes(new[] { poison }), Hashes(block.DroppedTransactions.Select(d => d.Transaction)));
            Assert.Empty(block.DeferredTransactions);
        }

        [Fact]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_APoisonTransactionIsOrderedAheadOfHonestOnes_When_ABlockIsMined_Then_TheHonestOnesAreIncludedInTheSameBlock()
        {
            var (node, accounts) = await StartAsync(3);
            var chainId = node.DevConfig.ChainId;
            var poison = Signed(accounts[0], chainId, UnwiredKzgPrecompile);
            var honestA = Signed(accounts[1], chainId, Honest);
            var honestB = Signed(accounts[2], chainId, Honest);
            node.BlockManager.AddPendingTransaction(poison);
            node.BlockManager.AddPendingTransaction(honestA);
            node.BlockManager.AddPendingTransaction(honestB);

            await node.BlockManager.MineBlockAsync();
            var block = node.BlockManager.LastBlockProductionResult;

            Assert.Equal(Hashes(new[] { honestA, honestB }), Hashes(block.IncludedTransactions));
            Assert.Equal(Hashes(new[] { poison }), Hashes(block.DroppedTransactions.Select(d => d.Transaction)));
            Assert.Empty(block.DeferredTransactions);
        }

        [Fact]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_ASenderWhoseFirstTransactionFaultsTheHost_When_ABlockIsMined_Then_ThatSendersLaterTransactionIsDeferredAndAnotherSendersIsIncluded()
        {
            var (node, accounts) = await StartAsync(2);
            var chainId = node.DevConfig.ChainId;
            var poison = Signed(accounts[0], chainId, UnwiredKzgPrecompile, 0);
            var laterOfSameSender = Signed(accounts[0], chainId, Honest, 1);
            var otherSender = Signed(accounts[1], chainId, Honest);
            node.BlockManager.AddPendingTransaction(poison);
            node.BlockManager.AddPendingTransaction(laterOfSameSender);
            node.BlockManager.AddPendingTransaction(otherSender);

            await node.BlockManager.MineBlockAsync();
            var block = node.BlockManager.LastBlockProductionResult;

            Assert.Equal(Hashes(new[] { otherSender }), Hashes(block.IncludedTransactions));
            Assert.Equal(Hashes(new[] { poison }), Hashes(block.DroppedTransactions.Select(d => d.Transaction)));
            Assert.Equal(Hashes(new[] { laterOfSameSender }), Hashes(block.DeferredTransactions));
        }

        [Fact]
        [Trait("Finding", "S4-R3-05")]
        public async Task Given_EightPoisonTransactionsBetweenHonestOnes_When_ABlockIsMined_Then_ItClosesAfterAtMostSixExecutionsWithThePrefixIncludedAndTheRestDeferredNotDropped()
        {
            var (node, accounts) = await StartAsync(10);
            var chainId = node.DevConfig.ChainId;
            var first = Signed(accounts[0], chainId, Honest);
            var poisons = Enumerable.Range(1, 8).Select(i => Signed(accounts[i], chainId, UnwiredKzgPrecompile)).ToList();
            var last = Signed(accounts[9], chainId, Honest);
            node.BlockManager.AddPendingTransaction(first);
            foreach (var poison in poisons) node.BlockManager.AddPendingTransaction(poison);
            node.BlockManager.AddPendingTransaction(last);

            await node.BlockManager.MineBlockAsync();
            var block = node.BlockManager.LastBlockProductionResult;
            var executions = block.DroppedTransactions.Count + 1;
            _out.WriteLine($"executions {executions}, included {block.IncludedTransactions.Count}, dropped {block.DroppedTransactions.Count}, deferred {block.DeferredTransactions.Count}");

            Assert.Equal(6, executions);
            Assert.Equal(Hashes(new[] { first }), Hashes(block.IncludedTransactions));
            Assert.Equal(Hashes(poisons.Take(5)), Hashes(block.DroppedTransactions.Select(d => d.Transaction)));
            Assert.Equal(Hashes(poisons.Skip(5).Append(last)), Hashes(block.DeferredTransactions));

            var dropped = block.DroppedTransactions.Count;
            var lastIncluded = false;
            for (var next = 0; next < 4 && !lastIncluded; next++)
            {
                await node.BlockManager.MineBlockAsync();
                var result = node.BlockManager.LastBlockProductionResult;
                Assert.True(result.DroppedTransactions.Count + 1 <= 6);
                dropped += result.DroppedTransactions.Count;
                lastIncluded = result.IncludedTransactions.Any(t => t.Hash.ToHex() == last.Hash.ToHex());
            }
            Assert.Equal(8, dropped);
            Assert.True(lastIncluded);
        }
    }
}
