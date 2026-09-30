using System;
using System.Collections.Generic;
using System.Text;
using Nethereum.CoreChain.Storage;
using Nethereum.Util;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class RocksDbChainMetadataStore : IChainMetadataStore
    {
        private readonly RocksDbManager _rocks;
        private readonly ISnapSyncStateEncoder _snapSyncEncoder;
        private readonly IHeaderSyncStateEncoder _headerSyncEncoder;

        public RocksDbChainMetadataStore(RocksDbManager rocks, ISnapSyncStateEncoder snapSyncEncoder = null,
            IHeaderSyncStateEncoder headerSyncEncoder = null)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
            _snapSyncEncoder = snapSyncEncoder ?? SnapSyncStateRlpEncoder.Instance;
            _headerSyncEncoder = headerSyncEncoder ?? HeaderSyncStateRlpEncoder.Instance;
        }

        public ulong GetLastBlock()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastBlock);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public byte[] GetLastBlockHash()
            => _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastBlockHash);

        public ulong GetLastFetchedHeader()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedHeader);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public void SetLastFetchedHeader(ulong blockNumber)
            => _rocks.Put(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedHeader, RocksDbManager.Write64BE(blockNumber));

        public ulong GetLastFetchedBody()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedBody);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public void SetLastFetchedBody(ulong blockNumber)
            => _rocks.Put(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedBody, RocksDbManager.Write64BE(blockNumber));

        public void SetLastFetchedHeaderAndBody(ulong headerBlock, ulong bodyBlock)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.LastFetchedHeader, RocksDbManager.Write64BE(headerBlock), cf);
            batch.Put(MetaKeys.LastFetchedBody, RocksDbManager.Write64BE(bodyBlock), cf);
            _rocks.Write(batch);
        }

        public ulong GetReceiptBackfillCursor()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.ReceiptBackfillCursor);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public void SetReceiptBackfillCursor(ulong blockNumber)
            => _rocks.Put(RocksDbManager.CF_METADATA, MetaKeys.ReceiptBackfillCursor, RocksDbManager.Write64BE(blockNumber));

        public void Commit(ulong lastBlock, byte[] lastBlockHash)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.LastBlock, RocksDbManager.Write64BE(lastBlock), cf);
            if (lastBlockHash != null && lastBlockHash.Length == 32)
                batch.Put(MetaKeys.LastBlockHash, lastBlockHash, cf);
            _rocks.Write(batch);
        }

        public ulong GetDurableStateBlock()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.DurableStateBlock);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public void CommitDurableState(ulong block, byte[] hash)
        {
            using var batch = _rocks.CreateWriteBatch();
            AddDurableStateToBatch(batch, block, hash);
            _rocks.Write(batch);
        }

        internal void AddDurableStateToBatch(WriteBatch batch, ulong block, byte[] hash)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            batch.Put(MetaKeys.DurableStateBlock, RocksDbManager.Write64BE(block), cf);
            if (hash != null && hash.Length == 32)
                batch.Put(MetaKeys.DurableStateHash, hash, cf);
        }

        internal void AddExecutedHeadToBatch(WriteBatch batch, ulong block, byte[] hash)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            batch.Put(MetaKeys.LastBlock, RocksDbManager.Write64BE(block), cf);
            if (hash != null && hash.Length == 32)
                batch.Put(MetaKeys.LastBlockHash, hash, cf);
        }

        public ulong GetPromotionCursor() => GetPromotionCursor(null);

        public ulong GetPromotionCursor(RocksDbSharp.ReadOptions readOptions)
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.PromotionCursor, readOptions);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public bool HasPromotionCursor()
            => _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.PromotionCursor) != null;

        internal void AddPromotionCursorToBatch(WriteBatch batch, ulong block)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            batch.Put(MetaKeys.PromotionCursor, RocksDbManager.Write64BE(block), cf);
        }

        public bool IsGenesisLoaded()
            => _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.GenesisLoaded) != null;

        public void MarkGenesisLoaded()
            => _rocks.Put(RocksDbManager.CF_METADATA, MetaKeys.GenesisLoaded, new byte[] { 1 });

        public void SaveCheckpoint(ulong blockNumber, byte[] stateRoot, byte[] blockHash)
        {
            if (stateRoot == null || stateRoot.Length != 32) throw new ArgumentException("stateRoot must be 32 bytes", nameof(stateRoot));
            if (blockHash == null || blockHash.Length != 32) throw new ArgumentException("blockHash must be 32 bytes", nameof(blockHash));

            var payload = new byte[72];
            Buffer.BlockCopy(stateRoot, 0, payload, 0, 32);
            Buffer.BlockCopy(blockHash, 0, payload, 32, 32);
            var ts = RocksDbManager.Write64BE((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Buffer.BlockCopy(ts, 0, payload, 64, 8);

            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(CheckpointKey(blockNumber), payload, cf);
            batch.Put(MetaKeys.CheckpointLatest, RocksDbManager.Write64BE(blockNumber), cf);
            _rocks.Write(batch);
            _rocks.Flush();
        }

        public ulong GetLatestCheckpoint()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.CheckpointLatest);
            return raw == null || raw.Length != 8 ? 0UL : RocksDbManager.Read64BE(raw);
        }

        public ChainCheckpoint? GetCheckpoint(ulong blockNumber)
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, CheckpointKey(blockNumber));
            if (raw == null || raw.Length != 72) return null;
            var sr = new byte[32]; Buffer.BlockCopy(raw, 0, sr, 0, 32);
            var bh = new byte[32]; Buffer.BlockCopy(raw, 32, bh, 0, 32);
            var ts = new byte[8];  Buffer.BlockCopy(raw, 64, ts, 0, 8);
            return new ChainCheckpoint(blockNumber, sr, bh, RocksDbManager.Read64BE(ts));
        }

        public ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong upToBlock)
        {
            var targetKey = CheckpointKey(upToBlock);
            using var it = _rocks.CreateIterator(RocksDbManager.CF_METADATA);
            it.SeekForPrev(targetKey);
            if (!it.Valid()) return null;
            var key = it.Key();
            if (!HasCheckpointPrefix(key)) return null;
            ulong blockNumber = ParseCheckpointKey(key);
            if (blockNumber > upToBlock) return null;
            var raw = it.Value();
            if (raw == null || raw.Length != 72) return null;
            var sr = new byte[32]; Buffer.BlockCopy(raw, 0, sr, 0, 32);
            var bh = new byte[32]; Buffer.BlockCopy(raw, 32, bh, 0, 32);
            var ts = new byte[8];  Buffer.BlockCopy(raw, 64, ts, 0, 8);
            return new ChainCheckpoint(blockNumber, sr, bh, RocksDbManager.Read64BE(ts));
        }

        public ChainCheckpoint RewindToCheckpointAtOrBefore(ulong targetBlock)
        {
            var cp = GetNearestCheckpointAtOrBefore(targetBlock)
                ?? throw new InvalidOperationException(
                    $"No checkpoint at or below block {targetBlock:N0}; cannot rewind.");
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.LastBlock, RocksDbManager.Write64BE(cp.BlockNumber), cf);
            batch.Put(MetaKeys.LastBlockHash, cp.BlockHash, cf);
            var rewindBE = RocksDbManager.Write64BE(cp.BlockNumber);
            if (GetLastFetchedHeader() > cp.BlockNumber)
                batch.Put(MetaKeys.LastFetchedHeader, rewindBE, cf);
            if (GetLastFetchedBody() > cp.BlockNumber)
                batch.Put(MetaKeys.LastFetchedBody, rewindBE, cf);
            DeleteCheckpointsAboveIntoBatch(cp.BlockNumber, batch, cf);
            _rocks.Write(batch);
            _rocks.Flush();
            return cp;
        }

        public System.Collections.Generic.IReadOnlyList<ulong> ListCheckpointBlockNumbers()
        {
            var result = new System.Collections.Generic.List<ulong>();
            using var it = _rocks.CreateIterator(RocksDbManager.CF_METADATA);
            var prefix = System.Text.Encoding.ASCII.GetBytes("cp_");
            it.Seek(prefix);
            while (it.Valid())
            {
                var key = it.Key();
                if (!HasCheckpointPrefix(key)) break;
                try { result.Add(ParseCheckpointKey(key)); }
                catch (InvalidOperationException) { }
                it.Next();
            }
            return result;
        }

        public void DeleteCheckpoint(ulong blockNumber)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Delete(CheckpointKey(blockNumber), cf);
            ulong newLatest = 0;
            foreach (var bn in ListCheckpointBlockNumbers())
                if (bn != blockNumber && bn > newLatest) newLatest = bn;
            batch.Put(MetaKeys.CheckpointLatest, RocksDbManager.Write64BE(newLatest), cf);
            _rocks.Write(batch);
            _rocks.Flush();
        }

        public int DeleteCheckpointsAbove(ulong targetBlock)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            var removed = DeleteCheckpointsAboveIntoBatch(targetBlock, batch, cf);
            if (removed == 0) return 0;
            _rocks.Write(batch);
            _rocks.Flush();
            return removed;
        }

        private int DeleteCheckpointsAboveIntoBatch(
            ulong targetBlock, RocksDbSharp.WriteBatch batch, RocksDbSharp.ColumnFamilyHandle cf)
        {
            int removed = 0;
            ulong priorLatest = GetLatestCheckpoint();
            ulong newLatestCandidate = priorLatest <= targetBlock ? priorLatest : 0;
            foreach (var bn in ListCheckpointBlockNumbers())
            {
                if (bn > targetBlock)
                {
                    batch.Delete(CheckpointKey(bn), cf);
                    removed++;
                }
                else if (bn > newLatestCandidate)
                {
                    newLatestCandidate = bn;
                }
            }
            if (removed > 0 && priorLatest > targetBlock)
                batch.Put(MetaKeys.CheckpointLatest, RocksDbManager.Write64BE(newLatestCandidate), cf);
            return removed;
        }

        public void ResetForStateRebuild()
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Delete(MetaKeys.LastBlock, cf);
            batch.Delete(MetaKeys.LastBlockHash, cf);
            batch.Delete(MetaKeys.DurableStateBlock, cf);
            batch.Delete(MetaKeys.DurableStateHash, cf);
            batch.Delete(MetaKeys.LastFetchedHeader, cf);
            batch.Delete(MetaKeys.LastFetchedBody, cf);
            batch.Delete(MetaKeys.GenesisLoaded, cf);
            batch.Delete(MetaKeys.CheckpointLatest, cf);
            batch.Delete(MetaKeys.SnapSyncState, cf);
            foreach (var bn in ListCheckpointBlockNumbers())
            {
                batch.Delete(CheckpointKey(bn), cf);
            }
            _rocks.Write(batch);
            _rocks.Flush();
        }

        public SnapSyncState GetSnapSyncState()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.SnapSyncState);
            if (raw == null || raw.Length == 0) return null;
            try { return _snapSyncEncoder.Decode(raw); }
            catch { return null; }
        }

        public void SaveSnapSyncState(SnapSyncState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var blob = _snapSyncEncoder.Encode(state);
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.SnapSyncState, blob, cf);
            _rocks.Write(batch);
        }

        public void ClearSnapSyncState()
        {
            _rocks.Delete(RocksDbManager.CF_METADATA, MetaKeys.SnapSyncState);
        }

        public void ClearCommittedHead()
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Delete(MetaKeys.LastBlock, cf);
            batch.Delete(MetaKeys.LastBlockHash, cf);
            batch.Delete(MetaKeys.DurableStateBlock, cf);
            batch.Delete(MetaKeys.DurableStateHash, cf);
            _rocks.Write(batch);
        }

        public byte[] GetLightClientStateBlob()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LightClientState);
            return raw != null && raw.Length > 0 ? raw : null;
        }

        public void SaveLightClientStateBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("blob required", nameof(blob));
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.LightClientState, blob, cf);
            _rocks.Write(batch);
        }

        public HeaderSyncState GetHeaderSyncState()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.HeaderSyncState);
            if (raw == null || raw.Length == 0) return HeaderSyncState.Empty;
            try { return _headerSyncEncoder.Decode(raw) ?? HeaderSyncState.Empty; }
            catch { return HeaderSyncState.Empty; }
        }

        public void SaveHeaderSyncState(HeaderSyncState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            var blob = _headerSyncEncoder.Encode(state);
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.HeaderSyncState, blob, cf);
            _rocks.Write(batch);
        }

        public byte[] GetDeferredHealAccountsBlob()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.DeferredHealAccounts);
            return raw != null && raw.Length > 0 ? raw : null;
        }

        public void SaveDeferredHealAccountsBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("blob required", nameof(blob));
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.DeferredHealAccounts, blob, cf);
            _rocks.Write(batch);
        }

        public void ClearDeferredHealAccountsBlob()
        {
            _rocks.Delete(RocksDbManager.CF_METADATA, MetaKeys.DeferredHealAccounts);
        }

        public byte[] GetDeferredHealCodeBlob()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.DeferredHealCode);
            return raw != null && raw.Length > 0 ? raw : null;
        }

        public void SaveDeferredHealCodeBlob(byte[] blob)
        {
            if (blob == null || blob.Length == 0) throw new ArgumentException("blob required", nameof(blob));
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.DeferredHealCode, blob, cf);
            _rocks.Write(batch);
        }

        public void ClearDeferredHealCodeBlob()
        {
            _rocks.Delete(RocksDbManager.CF_METADATA, MetaKeys.DeferredHealCode);
        }

        public void UpsertDeferredStorageDebt(DeferredStorageDebt debt)
        {
            DeferredStorageDebt.Validate(debt);
            _rocks.Put(
                RocksDbManager.CF_METADATA,
                DeferredStorageDebtKey(debt.AccountHash, debt.DiscoveredStorageRoot),
                DeferredStorageDebtCodec.Encode(debt));
        }

        public IReadOnlyList<DeferredStorageDebt> ListOpenDeferredStorageDebts(int max = int.MaxValue)
        {
            if (max <= 0) return Array.Empty<DeferredStorageDebt>();
            var result = new List<DeferredStorageDebt>();
            using var it = _rocks.CreateIterator(RocksDbManager.CF_METADATA);
            it.Seek(MetaKeys.DeferredStorageDebtPrefix);
            while (it.Valid())
            {
                var key = it.Key();
                if (!HasDeferredStorageDebtPrefix(key)) break;

                try
                {
                    var debt = DeferredStorageDebtCodec.Decode(it.Value());
                    if (debt.IsOpen)
                    {
                        result.Add(debt);
                        if (result.Count >= max) break;
                    }
                }
                catch
                {
                }

                it.Next();
            }
            return result;
        }

        public ulong CountOpenDeferredStorageDebts()
        {
            ulong count = 0;
            using var it = _rocks.CreateIterator(RocksDbManager.CF_METADATA);
            it.Seek(MetaKeys.DeferredStorageDebtPrefix);
            while (it.Valid())
            {
                var key = it.Key();
                if (!HasDeferredStorageDebtPrefix(key)) break;

                try
                {
                    if (DeferredStorageDebtCodec.Decode(it.Value()).IsOpen) count++;
                }
                catch
                {
                    count++;
                }

                it.Next();
            }
            return count;
        }

        public void ClearDeferredStorageDebt(byte[] accountHash, byte[] discoveredStorageRoot)
        {
            _rocks.Delete(RocksDbManager.CF_METADATA, DeferredStorageDebtKey(accountHash, discoveredStorageRoot));
        }

        public void ClearAllDeferredStorageDebts()
        {
            var keys = new List<byte[]>();
            using (var it = _rocks.CreateIterator(RocksDbManager.CF_METADATA))
            {
                it.Seek(MetaKeys.DeferredStorageDebtPrefix);
                while (it.Valid())
                {
                    var key = it.Key();
                    if (!HasDeferredStorageDebtPrefix(key)) break;
                    keys.Add(key);
                    it.Next();
                }
            }
            foreach (var key in keys)
                _rocks.Delete(RocksDbManager.CF_METADATA, key);
        }

        private static bool HasCheckpointPrefix(byte[] key)
            => key != null && key.Length == 3 + 16 && key[0] == (byte)'c' && key[1] == (byte)'p' && key[2] == (byte)'_';

        private static bool HasDeferredStorageDebtPrefix(byte[] key)
        {
            if (key == null || key.Length < MetaKeys.DeferredStorageDebtPrefix.Length) return false;
            for (int i = 0; i < MetaKeys.DeferredStorageDebtPrefix.Length; i++)
                if (key[i] != MetaKeys.DeferredStorageDebtPrefix[i]) return false;
            return true;
        }
        private static ulong ParseCheckpointKey(byte[] key)
        {
            ulong v = 0;
            for (int i = 3; i < 3 + 16; i++)
            {
                byte c = key[i];
                int d = c >= (byte)'0' && c <= (byte)'9' ? c - (byte)'0'
                      : c >= (byte)'A' && c <= (byte)'F' ? c - (byte)'A' + 10
                      : -1;
                if (d < 0)
                    throw new InvalidOperationException(
                        $"Corrupted checkpoint key: '{System.Text.Encoding.ASCII.GetString(key)}' contains non-hex byte 0x{c:X2} at index {i}.");
                v = (v << 4) | (ulong)(uint)d;
            }
            return v;
        }

        private static byte[] CheckpointKey(ulong blockNumber)
            => System.Text.Encoding.ASCII.GetBytes("cp_" + blockNumber.ToString("X16"));

        public static byte[] DeferredStorageDebtKey(byte[] accountHash, byte[] discoveredStorageRoot)
        {
            if (accountHash == null || accountHash.Length != 32) throw new ArgumentException("accountHash must be 32 bytes", nameof(accountHash));
            if (discoveredStorageRoot == null || discoveredStorageRoot.Length != 32) throw new ArgumentException("discoveredStorageRoot must be 32 bytes", nameof(discoveredStorageRoot));
            return ByteUtil.Merge(MetaKeys.DeferredStorageDebtPrefix, accountHash, discoveredStorageRoot);
        }
        public static class MetaKeys
        {
            public static readonly byte[] LastBlock = System.Text.Encoding.ASCII.GetBytes("last_block");
            public static readonly byte[] LastBlockHash = System.Text.Encoding.ASCII.GetBytes("last_block_hash");
            public static readonly byte[] DurableStateBlock = System.Text.Encoding.ASCII.GetBytes("durable_state_block");
            public static readonly byte[] DurableStateHash = System.Text.Encoding.ASCII.GetBytes("durable_state_hash");
            public static readonly byte[] LastFetchedHeader = System.Text.Encoding.ASCII.GetBytes("last_fetched_header");
            public static readonly byte[] LastFetchedBody = System.Text.Encoding.ASCII.GetBytes("last_fetched_body");
            public static readonly byte[] GenesisLoaded = System.Text.Encoding.ASCII.GetBytes("genesis_loaded");
            public static readonly byte[] CheckpointLatest = System.Text.Encoding.ASCII.GetBytes("meta_last_cp");
            public static readonly byte[] SnapSyncState = System.Text.Encoding.ASCII.GetBytes("snap_sync_state");
            public static readonly byte[] LightClientState = System.Text.Encoding.ASCII.GetBytes("lc_state");
            public static readonly byte[] HeaderSyncState = System.Text.Encoding.ASCII.GetBytes("header_sync_state");
            public static readonly byte[] ReceiptBackfillCursor = System.Text.Encoding.ASCII.GetBytes("receipt_backfill_cursor");
            public static readonly byte[] PromotionCursor = System.Text.Encoding.ASCII.GetBytes("promotion_cursor");
            public static readonly byte[] DeferredHealAccounts = System.Text.Encoding.ASCII.GetBytes("deferred_heal_accounts");
            public static readonly byte[] DeferredHealCode = System.Text.Encoding.ASCII.GetBytes("deferred_heal_code");
            public static readonly byte[] DeferredStorageDebtPrefix = Encoding.ASCII.GetBytes("dsd_");
        }
    }
}
