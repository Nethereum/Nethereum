using System;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.Storage
{
    public class FlatReconcileUnresolvableNodeException : InvalidOperationException
    {
        public static readonly byte[] WholeAccountTrieSentinel = new byte[32];

        public System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> Subtrees { get; }

        public FlatReconcileUnresolvableNodeException(
            string message,
            System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> subtrees,
            Exception inner)
            : base(message, inner)
        {
            Subtrees = subtrees ?? System.Array.Empty<(byte[], byte[])>();
        }
    }

    public interface IFlatStateReconciler
    {
        Task<FlatStateReconcileResult> ReconcileFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct);

        Task<FlatStateReconcileResult> VerifyFlatStateAsync(
            byte[] stateRoot, Action<string> progress, CancellationToken ct, long sampleAccountsPerShard = 0);

        System.Collections.Generic.IReadOnlyList<(byte[] AccountHash, byte[] StorageRoot)> GetPersistedDamage();

        void ClearPersistedDamage();

        System.Collections.Generic.IReadOnlyList<byte[]> GetPersistedMissingCode();

        void ClearPersistedMissingCode();
    }

    public sealed class FlatStateReconcileResult
    {
        public FlatStateReconcileResult(
            long accountsScanned, long slotsScanned,
            long ghostAccountsDeleted, long ghostSlotsDeleted,
            long accountsAdded, long slotsAdded,
            long accountsPatched, long slotsPatched)
        {
            AccountsScanned = accountsScanned;
            SlotsScanned = slotsScanned;
            GhostAccountsDeleted = ghostAccountsDeleted;
            GhostSlotsDeleted = ghostSlotsDeleted;
            AccountsAdded = accountsAdded;
            SlotsAdded = slotsAdded;
            AccountsPatched = accountsPatched;
            SlotsPatched = slotsPatched;
        }

        public long AccountsScanned { get; }
        public long SlotsScanned { get; }
        public long GhostAccountsDeleted { get; }
        public long GhostSlotsDeleted { get; }
        public long AccountsAdded { get; }
        public long SlotsAdded { get; }
        public long AccountsPatched { get; }
        public long SlotsPatched { get; }

        public long TotalRepairs =>
            GhostAccountsDeleted + GhostSlotsDeleted + AccountsAdded + SlotsAdded + AccountsPatched + SlotsPatched;
    }
}
