using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.State;
using Nethereum.DevP2P.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Chain.TestData.Vectors;
using Xunit;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Phase2;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.DevP2P.Sync.Snap.Storage;
using Nethereum.DevP2P.Sync.Snap.Sinks;
using Nethereum.DevP2P.Sync.Snap.Peers;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;

namespace Nethereum.Chain.TestData.UnitTests
{
    public class SnapSyncTests
    {
        [Fact]
        public async Task Follower_SnapSyncs_SequencerState_AndMatchesEveryAccount()
        {
            var sequencer = await InProcessSequencerDriver.CreateAsync(generatedAccounts: 100);
            await new WorkloadV1().BuildAsync(sequencer);

            var tip = await sequencer.Blocks.GetHeightAsync();
            var pivotHeader = await sequencer.Blocks.GetByNumberAsync(tip);
            var pivotHash = await sequencer.Blocks.GetHashByNumberAsync(tip);
            var pivotStateRoot = pivotHeader.StateRoot;

            var sequencerAccounts = await sequencer.State.GetAllAccountsAsync();
            var bytecodes = new Dictionary<string, byte[]>();
            foreach (var kv in sequencerAccounts)
            {
                var codeHash = kv.Value.CodeHash;
                if (codeHash == null || codeHash.Length == 0) continue;
                var code = await sequencer.State.GetCodeAsync(codeHash);
                if (code != null && code.Length > 0) bytecodes[codeHash.ToHex()] = code;
            }
            var handler = new PatriciaSnapRequestHandler(sequencer.TrieNodes, new DictBytecodeStore(bytecodes));
            var peer = new InProcessSnapPeer(handler);

            var dbPath = Path.Combine(Path.GetTempPath(), "snap-sync-" + Guid.NewGuid().ToString("N"));
            try
            {
                using var bundle = RocksDbChainStoreBundle.Open(dbPath);

                await bundle.Blocks.SaveAsync(pivotHeader, pivotHash);

                var result = await SnapBootstrapper.RunAsync(bundle, peer, pivotHeader, pivotHash, NullLogger.Instance);

                Assert.True(result.Ran, result.SkipReason);
                await result.HistoryBackfill;
                await result.StateCompaction.ConfigureAwait(false);
                Assert.Equal(pivotStateRoot.ToHex(), result.PivotStateRoot.ToHex());
                Assert.True(result.AccountCount > 100, $"only {result.AccountCount} accounts synced");

                var follower = new TrieFallbackStateStore(bundle.State, (INodeBlobStore)bundle.TrieNodes, () => pivotStateRoot);
                foreach (var kv in sequencerAccounts)
                {
                    var synced = await follower.GetAccountAsync(kv.Key);
                    Assert.NotNull(synced);
                    Assert.Equal(kv.Value.Balance, synced.Balance);
                    Assert.Equal(kv.Value.Nonce, synced.Nonce);
                    Assert.Equal(kv.Value.CodeHash, synced.CodeHash);
                    Assert.Equal(kv.Value.StateRoot, synced.StateRoot);
                }
            }
            finally
            {
                try { if (Directory.Exists(dbPath)) Directory.Delete(dbPath, true); } catch { }
            }
        }

        private sealed class DictBytecodeStore : IBytecodeStore
        {
            private readonly Dictionary<string, byte[]> _codes;
            public DictBytecodeStore(Dictionary<string, byte[]> codes) => _codes = codes;
            public byte[] Get(byte[] codeHash) => _codes.TryGetValue(codeHash.ToHex(), out var c) ? c : null;
        }
    }
}
