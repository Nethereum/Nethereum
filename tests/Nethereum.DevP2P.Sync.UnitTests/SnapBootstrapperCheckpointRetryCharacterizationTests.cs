using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperCheckpointRetryCharacterizationTests
    {
        private const long PivotBlock = 40;

        private sealed class NoCodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }

        private static async Task<(SnapBootstrapper.Result Result, CharBundle Bundle, CharLog Log)> RunAsync(int failingCheckpointAttempts)
        {
            var store = new InMemoryContentNodeStore();
            var keccak = Sha3Keccack.Current;
            var trie = new PatriciaTrie(store);
            for (byte i = 1; i <= 3; i++)
                trie.Put(keccak.CalculateHash(new[] { i }), new AccountEncoder().Encode(new Account
                {
                    Nonce = (EvmUInt256)i,
                    Balance = (EvmUInt256)(i * 10u),
                    StateRoot = DefaultValues.EMPTY_TRIE_HASH,
                    CodeHash = DefaultValues.EMPTY_DATA_HASH,
                }));
            trie.SaveDirtyNodesToStorage();
            var root = trie.Root.GetHash();

            var bundle = new CharBundle();
            var pivotHash = keccak.CalculateHash(new byte[] { 0x40 });
            var pivot = new BlockHeader { BlockNumber = PivotBlock, StateRoot = root, ParentHash = CharBytes.Hash32(0x09) };
            await bundle.Blocks.SaveAsync(pivot, pivotHash);
            var attempts = 0;
            bundle.SaveCheckpointHook = (block, stateRoot, hash, ct) =>
                ++attempts <= failingCheckpointAttempts
                    ? throw new System.IO.IOException("disk full")
                    : Task.FromResult(new ChainCheckpoint());

            var log = new CharLog();
            var result = await SnapBootstrapper.RunAsync(
                bundle, new InProcessSnapPeer(new PatriciaSnapRequestHandler(store, new NoCodes())),
                pivot, pivotHash, log,
                new SnapRunOptions { RunBackfill = false, UseBackwardSkeleton = false, AccountConcurrency = 1 },
                CancellationToken.None);
            return (result, bundle, log);
        }

        [Fact]
        public async Task Given_TheCheckpointSaveFailsTwice_When_ThePivotIsPersisted_Then_ItIsRetriedAndPersistedOnTheThirdAttempt()
        {
            var (result, bundle, log) = await RunAsync(failingCheckpointAttempts: 2);
            using var _ = bundle;

            Assert.True(result.Ran);
            Assert.Equal(3, bundle.SaveCheckpointCalls);
            var warnings = log.With("pivot checkpoint save failed");
            Assert.Equal(2, warnings.Count);
            Assert.All(warnings, w => Assert.Equal(LogLevel.Warning, w.Level));
            Assert.Equal("Snap-bootstrap: pivot checkpoint save failed at block 40 (attempt 1/3); retrying.", warnings[0].Message);
            Assert.Equal("Snap-bootstrap: pivot checkpoint save failed at block 40 (attempt 2/3); retrying.", warnings[1].Message);
            Assert.IsType<System.IO.IOException>(warnings[0].Exception);
            Assert.Equal(
                "Snap-bootstrap: pivot (heal-complete) checkpoint persisted at block 40 (attempt 3).",
                Assert.Single(log.With("checkpoint persisted at block")).Message);
            Assert.False(log.Saw("could NOT be persisted"));
        }

        [Fact]
        public async Task Given_TheCheckpointSaveAlwaysFails_When_ThePivotIsPersisted_Then_ItLogsCriticalAfterThreeAttemptsAndTheRunStillCompletes()
        {
            var (result, bundle, log) = await RunAsync(failingCheckpointAttempts: int.MaxValue);
            using var _ = bundle;

            Assert.True(result.Ran);
            Assert.Equal(3, bundle.SaveCheckpointCalls);
            Assert.Equal(2, log.With("pivot checkpoint save failed").Count);
            var critical = Assert.Single(log.With("could NOT be persisted"));
            Assert.Equal(LogLevel.Critical, critical.Level);
            Assert.StartsWith(
                "Snap-bootstrap: pivot checkpoint could NOT be persisted at block 40 after 3 attempts. ", critical.Message);
            Assert.IsType<System.IO.IOException>(critical.Exception);
            Assert.False(log.Saw("checkpoint persisted at block"));
            Assert.Equal((ulong)PivotBlock, bundle.Metadata.GetLastBlock());
        }
    }
}
