using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class FlatStateReconciler : IFlatStateReconciler
    {
        private readonly FlatReconcileEnv _env;
        private readonly CertifyDiff _diff;
        private readonly FlatUnitPass _unitPass;

        public FlatStateReconciler(
            RocksDbManager rocks, RocksDbStateStore flatStore, ITrieNodeStore trieNodes, TimeProvider clock = null)
        {
            _env = new FlatReconcileEnv(
                rocks ?? throw new ArgumentNullException(nameof(rocks)),
                flatStore ?? throw new ArgumentNullException(nameof(flatStore)),
                trieNodes ?? throw new ArgumentNullException(nameof(trieNodes)),
                clock ?? TimeProvider.System);
            var proof = new TrieProof(trieNodes);
            _diff = new CertifyDiff(_env, proof);
            _unitPass = new FlatUnitPass(_env, new UnitJudge(_env, proof));
        }

        public Task<FlatStateReconcileResult> ReconcileFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct)
            => Certify(stateRoot, progress, ct, dryRun: false);

        public Task<FlatStateReconcileResult> VerifyFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0)
            => Certify(stateRoot, progress, ct, dryRun: true, sampleAccountsPerShard);

        public IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage()
            => _env.Inventory.GetDamage();

        public void ClearPersistedDamage() => _env.Inventory.ClearDamage();

        public IReadOnlyList<byte[]> GetPersistedMissingCode() => _env.Inventory.GetMissingCode();

        public void ClearPersistedMissingCode() => _env.Inventory.ClearMissingCode();

        public void ClearCleanShardMarkers(byte[] root) => CertifyJournal.DiscardAtRoot(_env.Rocks, root);

        void IFlatStateReconciler.DiscardCertifyJournal() => CertifyJournal.Discard(_env.Rocks);

        private Task<FlatStateReconcileResult> Certify(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, bool dryRun, long sampleAccountsPerShard = 0)
        {
            if (stateRoot == null || stateRoot.Length != 32)
                throw new ArgumentException("stateRoot must be 32 bytes", nameof(stateRoot));
            if (!_env.SupportsPathAddressedCertify)
                progress?.Invoke("certify.mode=leafwalk reason=no_path_store");
            else if (!_env.Flat.HasRlpAccountLayout)
                progress?.Invoke("certify.mode=leafwalk reason=binary_flat_layout");
            var byUnitHash = CanCertifyByUnitHash(sampleAccountsPerShard);
            return Task.Run(
                () => byUnitHash
                    ? CertifyByUnitHashAsync(stateRoot, progress, ct, dryRun)
                    : CertifyByLeafWalkAsync(stateRoot, progress, ct, dryRun, sampleAccountsPerShard),
                ct);
        }

        private bool CanCertifyByUnitHash(long sampleAccountsPerShard)
            => _env.SupportsPathAddressedCertify && _env.Flat.HasRlpAccountLayout && sampleAccountsPerShard == 0;

        private async Task<FlatStateReconcileResult> CertifyByLeafWalkAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, bool dryRun, long sampleAccountsPerShard)
        {
            using var queue = new WorkQueue(ct);
            var sw = Stopwatch.StartNew();
            var tally = new HashingTally(_env, progress, sw);
            var shards = new ShardResult[FlatStateWalk.ShardCount];
            for (int i = 0; i < FlatStateWalk.ShardCount; i++)
            {
                int shard = i;
                var (start, end) = FlatStateWalk.ShardBounds(shard);
                queue.Enqueue(() =>
                {
                    var walked = shards[shard] = ReconcileShard(stateRoot, start, end, queue.Token, dryRun, sampleAccountsPerShard);
                    tally.Walked(walked.Result.LeavesWalked);
                    progress?.Invoke(HashingTally.ShardProgressLine(
                        shard, walked.Result.AccountsScanned, walked.Result.SlotsScanned, walked.Result.TrieNodesMissing, 0, sw.Elapsed));
                });
            }
            await queue.DrainAsync(Math.Min(Environment.ProcessorCount, FlatStateWalk.ShardCount)).ConfigureAwait(false);
            if (!dryRun)
                foreach (var shard in shards) _env.Inventory.MergeMissingCode(shard.MissingCode);
            return ReconcileOutcome.Conclude(shards, null);
        }

        private ShardResult ReconcileShard(
            byte[] stateRoot, byte[] shardStart, byte[] shardEnd, CancellationToken ct, bool dryRun, long sampleAccountsPerShard)
        {
            var walk = new FlatLeafWalk(_env, ct, dryRun, sampleAccountsPerShard);
            walk.ReconcileAccountRange(stateRoot, shardStart, shardEnd);
            return walk.ToShardResult();
        }

        private async Task<FlatStateReconcileResult> CertifyByUnitHashAsync(
            byte[] root, Action<string> progress, CancellationToken ct, bool dryRun)
        {
            var sw = Stopwatch.StartNew();
            var journal = !dryRun && LastBlock() == 0;
            var results = new List<ShardResult>();
            if (journal)
            {
                CertifyJournal.StartIfAbsent(_env.Rocks, root);
                progress?.Invoke(StartLine(root));
                if (CertifyJournal.TrieChangedSince(_env.Rocks, root))
                    results.AddRange(await _diff.DiffEpochAsync(root, progress, ct).ConfigureAwait(false));
            }
            foreach (var unit in await _unitPass.RunAsync(root, dryRun, journal, progress, sw, ct).ConfigureAwait(false))
                results.Add(unit.ToShardResult());
            return ReconcileOutcome.Conclude(results, (totals, holes) => progress?.Invoke(DoneLine(totals, holes, sw.Elapsed)));
        }

        private string StartLine(byte[] root)
            => $"snap.flat.certify start root=0x{root.ToHex()} units_done={CertifyJournal.CountKeys(_env.Rocks, CertifyJournal.UnitDoneTag)}/{FlatUnitPass.UnitCount} " +
               $"captures={CertifyJournal.CountKeys(_env.Rocks, CertifyJournal.CaptureTag)} wipes={CertifyJournal.CountKeys(_env.Rocks, CertifyJournal.WipeTag)}";

        private static string DoneLine(FlatStateReconcileResult totals, int holes, TimeSpan elapsed)
            => $"snap.flat.certify done in {elapsed:hh\\:mm\\:ss} proof={(totals.RangesWalked == 0 ? "root" : "composed")} " +
               $"accounts={totals.AccountsScanned:N0} slots={totals.SlotsScanned:N0} " +
               $"added={totals.AccountsAdded:N0}/{totals.SlotsAdded:N0} ghost={totals.GhostAccountsDeleted:N0}/{totals.GhostSlotsDeleted:N0} " +
               $"patched={totals.AccountsPatched:N0}/{totals.SlotsPatched:N0} trie_missing={totals.TrieNodesMissing:N0} " +
               $"trie_differ={totals.TrieNodesDiffering:N0} unresolvable={holes:N0}";

        private ulong LastBlock()
        {
            var raw = _env.Rocks.Get(RocksDbManager.CF_METADATA, RocksDbChainMetadataStore.MetaKeys.LastBlock);
            return RocksDbChainMetadataStore.ReadCursor(raw);
        }
    }
}
