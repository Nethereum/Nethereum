using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.DevP2P.Sync.Serving;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.Sync.UnitTests
{
    public class SnapBootstrapperResumeTests
    {
        private static byte[] Fill(byte b)
        {
            var a = new byte[32];
            for (int i = 0; i < 32; i++) a[i] = b;
            return a;
        }

        [Fact]
        public async Task Given_ASavedHealStateAndNoScheduler_When_TheBootstrapResumes_Then_SnapRootMismatchExceptionNamesTheHealTarget()
        {
            using var bundle = InMemoryChainStoreBundle.Open();
            var pivotHash = Fill(0x11);
            var healTarget = Fill(0xAB);
            var codeHashes = new List<byte[]> { Fill(0xC1), Fill(0xC2) };
            bundle.Metadata.SaveSnapSyncState(new SnapSyncState
            {
                SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                Phase = SnapPhase.Phase3Running,
                PivotBlockNumber = 1,
                PivotBlockHash = pivotHash,
                HealTargetRoot = healTarget,
                Tasks = Array.Empty<SnapSyncAccountTask>(),
                Counters = SnapSyncCounters.Zero,
            });
            bundle.Metadata.SaveDeferredHealCodeBlob(NeedsHealCodeCodec.Encode(codeHashes));
            var pivot = new BlockHeader { BlockNumber = 1, StateRoot = Fill(0xCD) };
            var peer = new InProcessSnapPeer(new PatriciaSnapRequestHandler(new InMemoryContentNodeStore(), new NoCodes()));

            var thrown = await Assert.ThrowsAsync<SnapSyncClient.SnapRootMismatchException>(() =>
                SnapBootstrapper.RunAsync(
                    bundle, peer, pivot, pivotHash, NullLogger.Instance,
                    new SnapRunOptions { RunBackfill = false, UseBackwardSkeleton = false, AccountConcurrency = 1 }));

            Assert.Equal($"Snap resume at heal needs a request scheduler (persisted heal target 0x{healTarget.ToHex()})", thrown.Message);
            Assert.Empty(thrown.AccountsNeedingHeal);
            Assert.Equal(
                codeHashes.Select(h => h.ToHex()).OrderBy(h => h, StringComparer.Ordinal),
                thrown.CodeHashesNeedingHeal.Select(h => h.ToHex()).OrderBy(h => h, StringComparer.Ordinal));
        }

        private sealed class NoCodes : IBytecodeStore
        {
            public void Put(byte[] codeHash, byte[] code) { }
            public byte[] Get(byte[] codeHash) => null;
        }
    }
}
