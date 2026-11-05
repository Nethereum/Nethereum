using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class FlatReconcileEnv
    {
        internal const long ProgressSlotInterval = 10_000_000;
        private static readonly TimeSpan ProgressTimeInterval = TimeSpan.FromSeconds(60);

        public FlatReconcileEnv(RocksDbManager rocks, RocksDbStateStore flat, ITrieNodeStore trieNodes, TimeProvider clock)
        {
            Rocks = rocks;
            Flat = flat;
            TrieNodes = trieNodes;
            Clock = clock;
            Inventory = new FlatInventory(rocks);
        }

        public RocksDbManager Rocks { get; }
        public RocksDbStateStore Flat { get; }
        public ITrieNodeStore TrieNodes { get; }
        public TimeProvider Clock { get; }
        public FlatInventory Inventory { get; }

        public bool SupportsPathAddressedCertify
            => TrieNodes is IRawNodeReader && TrieNodes is IContractStorageWipeable;

        public bool IsProgressDue(long scanned, long countInterval, ref long lastReportedScanned, ref DateTimeOffset lastReportedAt)
        {
            if (scanned - lastReportedScanned >= countInterval)
            {
                lastReportedScanned = scanned;
                lastReportedAt = Clock.GetUtcNow();
                return true;
            }
            var now = Clock.GetUtcNow();
            if (now - lastReportedAt < ProgressTimeInterval)
                return false;
            lastReportedScanned = scanned;
            lastReportedAt = now;
            return true;
        }

        public void CollectMissingCode(Account account, HashSet<byte[]> codeSeen, HashSet<byte[]> missingCode)
        {
            if (account?.CodeHash is { Length: 32 } codeHash
                && !ByteUtil.AreEqual(codeHash, DefaultValues.EMPTY_DATA_HASH)
                && codeSeen.Add(codeHash)
                && Flat.GetCode(codeHash) is not { Length: > 0 })
            {
                missingCode.Add(codeHash);
            }
        }

        public void DeleteStorageRow(byte[] flatKey64)
        {
            Rocks.Delete(RocksDbManager.CF_STATE_STORAGE, flatKey64);
        }
    }
}
