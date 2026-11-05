using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.Storage;
using Nethereum.DevP2P.Sync.Snap.Bootstrap;
using Nethereum.DevP2P.Sync.Snap.Client;
using Nethereum.DevP2P.Sync.Snap.Healing;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;
using static Nethereum.Chain.TestData.UnitTests.Snap2LiveHarness;

namespace Nethereum.Chain.TestData.UnitTests
{
    public partial class WireHealTests
    {
        private async Task<(bool OwnerRequestedByConvergence, int NeedHealLines, int ForcedNeedHealLines, bool StorageTrieStored, byte[] Blob)> ResumePhase3WithAWipedWhaleAsync(bool withBlob)
        {
            await using var live = await StartLiveAsync(false, await BuildWorkloadAsync(false, WhaleShape.Static), staticTipAtHead: true);
            var whale = await StaticWhaleAsync(live);
            var copy = await CopyWithAFullJournalAsync(live);
            try
            {
                await using var resumed = OpenFollowerAt(copy);
                var saved = resumed.Bundle.Metadata.GetSnapSyncState();
                resumed.Bundle.Metadata.SaveSnapSyncState(new SnapSyncState
                {
                    SchemaVersion = SnapSyncStateRlpEncoder.CurrentSchemaVersion,
                    Phase = SnapPhase.Phase3Running,
                    PivotBlockNumber = saved.PivotBlockNumber,
                    PivotBlockHash = saved.PivotBlockHash,
                    HealTargetRoot = saved.HealTargetRoot,
                    Tasks = saved.Tasks,
                    Counters = saved.Counters,
                });
                ((IFlatStateReconciler)resumed.Bundle).DiscardCertifyJournal();
                var sink = ((IHealNodeSinkProvider)resumed.Bundle).CreateHealSink();
                sink.ForceWipeStorage(whale.Owner);
                sink.Flush();
                resumed.Bundle.Metadata.ClearCommittedHead();
                if (withBlob)
                    resumed.Bundle.Metadata.SaveDeferredHealAccountsBlob(
                        NeedsHealAccountsCodec.Encode(new[] { new SnapSyncClient.AccountNeedingHeal(whale.Owner, whale.StorageRoot) }));
                Assert.False(StoresStorageRoot(resumed.Manager, whale.Owner, whale.StorageRoot), "the whale's storage trie survived the wipe");
                Assert.DoesNotContain(whale.Owner.ToHex(), live.Scheduler.TrieNodeStorageOwners);

                var ownersAtConvergence = (string[])null;
                var log = live.NewLog();
                live.On(m =>
                {
                    if (m.Contains(Converged, StringComparison.Ordinal) && ownersAtConvergence == null)
                        ownersAtConvergence = live.Scheduler.TrieNodeStorageOwners.ToArray();
                });
                using var cts = new CancellationTokenSource(RunTimeout);

                await CompletedAsync(live.RunAsync(resumed, cts.Token));

                return (
                    ownersAtConvergence?.Contains(whale.Owner.ToHex()) ?? false,
                    log.Messages.Count(m => m.Contains("snap.phase3.needheal", StringComparison.Ordinal)),
                    log.Messages.Count(m => m.Contains("snap.phase3.needheal accounts=1", StringComparison.Ordinal)),
                    StoresStorageRoot(resumed.Manager, whale.Owner, whale.StorageRoot),
                    resumed.Bundle.Metadata.GetDeferredHealAccountsBlob());
            }
            finally
            {
                try { if (Directory.Exists(copy)) Directory.Delete(copy, true); } catch { }
            }
        }

        [Fact]
        public Task Given_ANeedHealAccountWhoseLeafMatchesTheHealTarget_When_Phase3Resumes_Then_ItsStorageTrieIsHealedAndTheBlobIsCleared()
            => Observed(async () =>
            {
                var outcome = await ResumePhase3WithAWipedWhaleAsync(withBlob: true);

                Assert.True(outcome.OwnerRequestedByConvergence, "the Phase-3 heal never requested the needs-heal account's storage nodes");
                Assert.True(outcome.StorageTrieStored, "the needs-heal account's storage trie was not healed");
                Assert.Equal(1, outcome.ForcedNeedHealLines);
                Assert.Null(outcome.Blob);
            });

        [Fact]
        public Task Given_AWipedStorageTrieWhoseAccountLeafMatchesTheHealTargetAndNoNeedHealBlob_When_Phase3Resumes_Then_NoPhase3TrieNodeRequestNamesTheOwner()
            => Observed(async () =>
            {
                var outcome = await ResumePhase3WithAWipedWhaleAsync(withBlob: false);

                Assert.False(outcome.OwnerRequestedByConvergence, "the unforced heal descended into the matching leaf");
                Assert.Equal(0, outcome.NeedHealLines);
            });
    }
}
