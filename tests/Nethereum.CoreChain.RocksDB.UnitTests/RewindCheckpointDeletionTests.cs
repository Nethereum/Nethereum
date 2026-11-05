using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Services;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.CoreChain.RocksDB.UnitTests
{
    public class RewindCheckpointDeletionTests : IDisposable
    {
        private const string AddrA = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
        private const int Head = 10;
        private const ulong Target = 6;

        private readonly string _dir = Path.Combine(Path.GetTempPath(), "necc-rewindcp-" + Guid.NewGuid().ToString("N"));

        public void Dispose()
        {
            if (Directory.Exists(_dir)) { try { Directory.Delete(_dir, true); } catch { } }
        }

        private static string ContractAddr(int i) => "0x" + i.ToString("x2") + new string('c', 38);

        private static byte[] Hash(byte fill)
        {
            var h = new byte[32];
            for (var i = 0; i < 32; i++) h[i] = fill;
            return h;
        }

        private static async Task SaveHeader(IBlockStore blocks, ulong number, byte[] hash, byte[] stateRoot)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = number == 0 ? new byte[32] : Hash((byte)(number - 1)),
                StateRoot = stateRoot,
                TransactionsHash = new byte[32],
                ReceiptHash = new byte[32],
                UnclesHash = new byte[32],
                ExtraData = Array.Empty<byte>(),
                LogsBloom = new byte[256],
                Coinbase = "0x0000000000000000000000000000000000000000",
                Difficulty = 0,
                GasLimit = 0,
                GasUsed = 0,
                Timestamp = 0,
                MixHash = new byte[32],
                Nonce = new byte[8]
            };
            await blocks.SaveAsync(header, hash);
        }

        private static async Task BuildChainWithCheckpointsAsync(RocksDbChainStoreBundle bundle, bool pathKeyed)
        {
            var hist = (HistoricalStateStore)bundle.State;
            var roots = new byte[Head + 1][];
            roots[0] = pathKeyed
                ? await new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes).ComputeStateRootAsync()
                : Hash(0xF0);
            await SaveHeader(bundle.Blocks, 0, Hash(0), roots[0]);

            for (var b = 1; b <= Head; b++)
            {
                if (pathKeyed) bundle.NodeCommitBlockSource.Arm((ulong)b);
                hist.SetCurrentBlockNumber(b);
                await hist.SaveAccountAsync(AddrA, new Account { Balance = 1000 * b, Nonce = b });
                var contract = ContractAddr(b);
                await hist.SaveAccountAsync(contract, new Account { Balance = 0, Nonce = 1 });
                await hist.SaveStorageAsync(contract, BigInteger.One, new byte[] { (byte)(0x10 + b) });
                roots[b] = pathKeyed
                    ? await new IncrementalStateRootCalculator(bundle.State, bundle.StateTrieNodes).ComputeStateRootAsync(roots[b - 1])
                    : Hash((byte)(0xF0 + b));
                await hist.ClearCurrentBlockNumberAsync();
                if (pathKeyed)
                {
                    bundle.NodeCommitBlockSource.Clear();
                    await ((IAtomicBlockFlush)bundle).FlushBlockAsync(null, (ulong)b, Hash((byte)b));
                    await ((IAtomicBlockFlush)bundle).DrainAsync();
                }

                await SaveHeader(bundle.Blocks, (ulong)b, Hash((byte)b), roots[b]);
                bundle.Metadata.Commit((ulong)b, Hash((byte)b));

                if (b == 4 || b == 8)
                    await bundle.SaveCheckpointAsync((ulong)b, roots[b], Hash((byte)b));
            }
        }

        [Fact]
        public async Task Given_CheckpointsAt4And8_When_ANodeHistoryRewindTo6Runs_Then_TheCheckpointAt8IsDeleted()
        {
            Directory.CreateDirectory(_dir);
            var options = new RocksDbStorageOptions
            {
                DatabasePath = _dir,
                PathKeyedState = true,
                TrieNodeHistoryBlocks = 16,
            };
            using var manager = new RocksDbManager(options);
            using var bundle = RocksDbChainStoreBundle.FromManager(
                manager, _dir, new HistoricalStateOptions { MaxHistoryBlocks = 16, EnablePruning = false }, ownsManager: false);

            await BuildChainWithCheckpointsAsync(bundle, pathKeyed: true);
            Assert.Equal(new ulong[] { 4, 8 }, (await bundle.ListCheckpointsAsync()).Select(c => c.BlockNumber).OrderBy(n => n).ToArray());

            var result = await new RewindCoordinator(bundle)
                .RewindToAsync(Target, RewindPolicy.JournalFirstThenSnapshot)
                .WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Equal(RewindOutcome.NodeHistoryUsed, result.Outcome);
            Assert.Equal(new ulong[] { 4 }, (await bundle.ListCheckpointsAsync()).Select(c => c.BlockNumber).OrderBy(n => n).ToArray());
        }

        [Fact]
        public async Task Given_CheckpointsAt4And8_When_AJournalRewindTo6RunsOnANonPathKeyedStore_Then_TheCheckpointAt8IsDeleted()
        {
            Directory.CreateDirectory(_dir);
            using var bundle = RocksDbChainStoreBundle.Open(
                _dir, new HistoricalStateOptions { MaxHistoryBlocks = 16, EnablePruning = false });

            await BuildChainWithCheckpointsAsync(bundle, pathKeyed: false);
            Assert.Equal(new ulong[] { 4, 8 }, (await bundle.ListCheckpointsAsync()).Select(c => c.BlockNumber).OrderBy(n => n).ToArray());

            var result = await new RewindCoordinator(bundle)
                .RewindToAsync(Target, RewindPolicy.JournalFirstThenSnapshot)
                .WaitAsync(TimeSpan.FromSeconds(60));

            Assert.Equal(RewindOutcome.JournalUsed, result.Outcome);
            Assert.Equal(new ulong[] { 4 }, (await bundle.ListCheckpointsAsync()).Select(c => c.BlockNumber).OrderBy(n => n).ToArray());
        }
    }
}
