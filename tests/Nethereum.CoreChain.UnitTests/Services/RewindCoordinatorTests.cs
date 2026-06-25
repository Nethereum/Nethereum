using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Services
{
    public class RewindCoordinatorTests
    {
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

        private static byte[] FillBytes(byte v) => Enumerable.Repeat(v, 32).ToArray();

        private static async Task SaveHeaderAsync(IBlockStore blocks, ulong number, byte[] hash)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
                StateRoot = new byte[32],
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = System.Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8],
            };
            await blocks.SaveAsync(header, hash);
        }

        [Fact]
        public async Task RewindToAsync_AlreadyAtTarget_ReturnsNoOp()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            bundle.Metadata.Commit(5, FillBytes(0x05));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(5, RewindPolicy.JournalFirstThenSnapshot);

            Assert.Equal(RewindOutcome.NoOp, result.Outcome);
            Assert.Equal(5UL, result.NewHead);
            Assert.Equal(0UL, result.UndoneCount);
            Assert.Null(result.RestoredCheckpoint);
        }

        [Fact]
        public async Task RewindToAsync_TargetAboveHead_ReturnsNoOp()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            bundle.Metadata.Commit(3, FillBytes(0x03));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(10, RewindPolicy.JournalFirstThenSnapshot);

            Assert.Equal(RewindOutcome.NoOp, result.Outcome);
            Assert.Equal(3UL, result.NewHead);
        }

        [Fact]
        public async Task RewindToAsync_JournalCoversTarget_RewindsViaJournal()
        {
            using var bundle = InMemoryChainStoreBundle.Open(HistoricalStateOptions.FullArchive);
            var journal = (IHistoricalStateProvider)bundle.State;

            await SaveHeaderAsync(bundle.Blocks, 0, FillBytes(0x00));
            await SaveHeaderAsync(bundle.Blocks, 1, FillBytes(0x11));
            await SaveHeaderAsync(bundle.Blocks, 2, FillBytes(0x22));

            await bundle.State.SaveAccountAsync(AddrA, new Account { Balance = 100, Nonce = 0 });
            bundle.Metadata.Commit(0, FillBytes(0x00));

            journal.SetCurrentBlockNumber(1);
            await bundle.State.SaveAccountAsync(AddrA, new Account { Balance = 200, Nonce = 1 });
            await journal.ClearCurrentBlockNumberAsync();
            bundle.Metadata.Commit(1, FillBytes(0x11));

            journal.SetCurrentBlockNumber(2);
            await bundle.State.SaveAccountAsync(AddrA, new Account { Balance = 300, Nonce = 2 });
            await journal.ClearCurrentBlockNumberAsync();
            bundle.Metadata.Commit(2, FillBytes(0x22));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(0, RewindPolicy.JournalFirstThenSnapshot);

            Assert.Equal(RewindOutcome.JournalUsed, result.Outcome);
            Assert.Equal(0UL, result.NewHead);
            Assert.Equal(2UL, result.UndoneCount);
            Assert.Null(result.RestoredCheckpoint);
            Assert.Equal(0UL, bundle.Metadata.GetLastBlock());

            var restored = await bundle.State.GetAccountAsync(AddrA);
            Assert.Equal(100, restored.Balance);
        }

        [Fact]
        public async Task RewindToAsync_NoJournalNoSnapshot_ReturnsNoPathAvailable()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(5, FillBytes(0x05));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(3, RewindPolicy.JournalFirstThenSnapshot);

            Assert.Equal(RewindOutcome.NoPathAvailable, result.Outcome);
            Assert.Equal(5UL, result.NewHead);
            Assert.Null(result.UndoneCount);
            Assert.Null(result.RestoredCheckpoint);
        }

        [Fact]
        public async Task RewindToAsync_JournalOnlyPolicy_DoesNotConsiderSnapshot()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            bundle.Metadata.Commit(10, FillBytes(0x0A));
            bundle.Metadata.SaveCheckpoint(5, FillBytes(0x55), FillBytes(0x66));

            var coordinator = new RewindCoordinator(bundle);

            var result = await coordinator.RewindToAsync(5, RewindPolicy.JournalOnly);

            Assert.Equal(RewindOutcome.NoPathAvailable, result.Outcome);
            Assert.Null(result.RestoredCheckpoint);
        }
    }
}
