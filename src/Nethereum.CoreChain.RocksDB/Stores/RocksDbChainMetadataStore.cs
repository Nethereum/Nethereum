using System;
using Nethereum.CoreChain.Storage;
using RocksDbSharp;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    /// <summary>
    /// RocksDB-backed <see cref="IChainMetadataStore"/>. Lives in the existing
    /// <see cref="RocksDbManager.CF_METADATA"/> column family — no new CF to
    /// migrate. Checkpoints keyed as <c>cp_BBBBBBBBBBBBBBBB</c> (16 hex chars
    /// of big-endian ulong) → 72-byte payload (stateRoot || blockHash ||
    /// unix-timestamp big-endian).
    /// </summary>
    public sealed class RocksDbChainMetadataStore : IChainMetadataStore
    {
        private readonly RocksDbManager _rocks;

        public RocksDbChainMetadataStore(RocksDbManager rocks)
        {
            _rocks = rocks ?? throw new ArgumentNullException(nameof(rocks));
        }

        public ulong GetLastBlock()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastBlock);
            return raw == null || raw.Length != 8 ? 0UL : Read64BE(raw);
        }

        public byte[] GetLastBlockHash()
            => _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastBlockHash);

        public ulong GetLastFetchedHeader()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedHeader);
            return raw == null || raw.Length != 8 ? 0UL : Read64BE(raw);
        }

        public void SetLastFetchedHeader(ulong blockNumber)
            => _rocks.Put(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedHeader, Write64BE(blockNumber));

        public ulong GetLastFetchedBody()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedBody);
            return raw == null || raw.Length != 8 ? 0UL : Read64BE(raw);
        }

        public void SetLastFetchedBody(ulong blockNumber)
            => _rocks.Put(RocksDbManager.CF_METADATA, MetaKeys.LastFetchedBody, Write64BE(blockNumber));

        public void Commit(ulong lastBlock, byte[] lastBlockHash)
        {
            // Both fields land via one WriteBatch so a kill between the two
            // Puts can't leave last_block and last_block_hash describing
            // different blocks. RocksDB guarantees the batch is all-or-nothing
            // at the WAL level, matching the "Atomically write" contract on
            // the interface XML doc.
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.LastBlock, Write64BE(lastBlock), cf);
            if (lastBlockHash != null && lastBlockHash.Length == 32)
                batch.Put(MetaKeys.LastBlockHash, lastBlockHash, cf);
            _rocks.Write(batch);
            // No explicit Flush — Commit runs every block, and the WriteBatch
            // is durable via the WAL. Per-block fsync would dominate sync
            // throughput on slow disks. RocksDB's WAL guarantees the batch
            // survives a crash; full SST flush happens on the next periodic
            // memtable flush or graceful shutdown.
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
            var ts = Write64BE((ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds());
            Buffer.BlockCopy(ts, 0, payload, 64, 8);

            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(CheckpointKey(blockNumber), payload, cf);
            batch.Put(MetaKeys.CheckpointLatest, Write64BE(blockNumber), cf);
            _rocks.Write(batch);
            _rocks.Flush();
        }

        public ulong GetLatestCheckpoint()
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, MetaKeys.CheckpointLatest);
            return raw == null || raw.Length != 8 ? 0UL : Read64BE(raw);
        }

        public ChainCheckpoint? GetCheckpoint(ulong blockNumber)
        {
            var raw = _rocks.Get(RocksDbManager.CF_METADATA, CheckpointKey(blockNumber));
            if (raw == null || raw.Length != 72) return null;
            var sr = new byte[32]; Buffer.BlockCopy(raw, 0, sr, 0, 32);
            var bh = new byte[32]; Buffer.BlockCopy(raw, 32, bh, 0, 32);
            var ts = new byte[8];  Buffer.BlockCopy(raw, 64, ts, 0, 8);
            return new ChainCheckpoint(blockNumber, sr, bh, Read64BE(ts));
        }

        public ChainCheckpoint? GetNearestCheckpointAtOrBefore(ulong upToBlock)
        {
            // Checkpoint keys are ASCII "cp_" + 16-char uppercase hex of block
            // number. Lex order matches numeric order because the hex is fixed
            // width and zero-padded, so SeekForPrev on cp_<upToBlock> returns
            // the largest checkpoint at or below the target.
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
            return new ChainCheckpoint(blockNumber, sr, bh, Read64BE(ts));
        }

        public ChainCheckpoint RewindToCheckpointAtOrBefore(ulong targetBlock)
        {
            var cp = GetNearestCheckpointAtOrBefore(targetBlock)
                ?? throw new InvalidOperationException(
                    $"No checkpoint at or below block {targetBlock:N0}; cannot rewind.");
            // Same atomicity guarantee as Commit: one WriteBatch so a kill
            // can't leave the metadata describing two different blocks.
            // Also clamps the pipeline cursors down — anything past the
            // rewind target is on a potentially-wrong fork and must be
            // re-fetched, so HeaderFetcher / BodyFetcher resume from the
            // rewound block. Orphan checkpoints above the rewound-to block
            // are dropped in the same batch so GetNearestCheckpointAtOrBefore
            // can't later return a stale-fork snapshot.
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Put(MetaKeys.LastBlock, Write64BE(cp.BlockNumber), cf);
            batch.Put(MetaKeys.LastBlockHash, cp.BlockHash, cf);
            var rewindBE = Write64BE(cp.BlockNumber);
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
            // Seek to first "cp_" prefix key. ASCII 'c'=99 < 'l' (last_block)
            // < 'm' (meta_last_cp), so cp_ keys come first in lex order — but
            // a defensive Seek to the literal "cp_" prefix avoids assumptions.
            var prefix = System.Text.Encoding.ASCII.GetBytes("cp_");
            it.Seek(prefix);
            while (it.Valid())
            {
                var key = it.Key();
                if (!HasCheckpointPrefix(key)) break;
                // ListCheckpointBlockNumbers is enumeration: a single corrupted row
                // shouldn't tank the entire list. Skip the bad row so callers can
                // still operate on the rest (cleanup paths in particular).
                try { result.Add(ParseCheckpointKey(key)); }
                catch (InvalidOperationException) { /* skip corrupted cp_ row */ }
                it.Next();
            }
            return result;
        }

        public void DeleteCheckpoint(ulong blockNumber)
        {
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Delete(CheckpointKey(blockNumber), cf);
            // Recompute latest. Cheap because checkpoints are sparse (every
            // N blocks via --checkpoint-every) and only stored a few thousand
            // even for full mainnet history. The recompute uses the iterator
            // BEFORE the batch commits, but the deleted key hasn't been
            // visible yet either — ListCheckpointBlockNumbers still includes
            // the deleted block. Filter it out so newLatest excludes the
            // record being removed in this same batch.
            ulong newLatest = 0;
            foreach (var bn in ListCheckpointBlockNumbers())
                if (bn != blockNumber && bn > newLatest) newLatest = bn;
            batch.Put(MetaKeys.CheckpointLatest, Write64BE(newLatest), cf);
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

        // Worker used by both RewindToCheckpointAtOrBefore (which folds the
        // delete into the rewind's own atomic batch) and the public
        // DeleteCheckpointsAbove entry-point. Returns how many cp_ rows were
        // staged for delete; refreshes CheckpointLatest when the prior latest
        // was among them. Caller owns batch lifetime + commit.
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
                batch.Put(MetaKeys.CheckpointLatest, Write64BE(newLatestCandidate), cf);
            return removed;
        }

        public void ResetForStateRebuild()
        {
            // Wipe last_block / last_block_hash / genesis_loaded / latest_cp,
            // and drop every checkpoint record. Keep nothing in the metadata CF
            // that depends on state. Headers + txs + uncles in other CFs are
            // untouched. All deletes go through one WriteBatch so a kill
            // mid-reset leaves the metadata fully intact (no half-wiped state
            // that would confuse the next start).
            var cf = _rocks.GetColumnFamily(RocksDbManager.CF_METADATA);
            using var batch = _rocks.CreateWriteBatch();
            batch.Delete(MetaKeys.LastBlock, cf);
            batch.Delete(MetaKeys.LastBlockHash, cf);
            batch.Delete(MetaKeys.LastFetchedHeader, cf);
            batch.Delete(MetaKeys.LastFetchedBody, cf);
            batch.Delete(MetaKeys.GenesisLoaded, cf);
            batch.Delete(MetaKeys.CheckpointLatest, cf);
            foreach (var bn in ListCheckpointBlockNumbers())
            {
                batch.Delete(CheckpointKey(bn), cf);
            }
            _rocks.Write(batch);
            _rocks.Flush();
        }

        private static bool HasCheckpointPrefix(byte[] key)
            => key != null && key.Length == 3 + 16 && key[0] == (byte)'c' && key[1] == (byte)'p' && key[2] == (byte)'_';

        // Throws on invalid hex rather than silently returning 0 (which would
        // promote a corrupted cp_XXX row to a phantom "block 0 checkpoint" that
        // GetNearestCheckpointAtOrBefore could happily return). Callers that
        // need best-effort iteration over a possibly-corrupted CF should wrap
        // in try/catch and skip the row — see ListCheckpointBlockNumbers.
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

        private static ulong Read64BE(byte[] b)
        {
            if (b == null || b.Length < 8)
                throw new ArgumentException(
                    $"Read64BE requires at least 8 bytes, got {b?.Length ?? 0}.", nameof(b));
            ulong v = 0;
            for (int i = 0; i < 8; i++) v = (v << 8) | b[i];
            return v;
        }

        private static byte[] Write64BE(ulong v)
        {
            var b = new byte[8];
            for (int i = 7; i >= 0; i--) { b[i] = (byte)(v & 0xff); v >>= 8; }
            return b;
        }

        internal static class MetaKeys
        {
            public static readonly byte[] LastBlock = System.Text.Encoding.ASCII.GetBytes("last_block");
            public static readonly byte[] LastBlockHash = System.Text.Encoding.ASCII.GetBytes("last_block_hash");
            public static readonly byte[] LastFetchedHeader = System.Text.Encoding.ASCII.GetBytes("last_fetched_header");
            public static readonly byte[] LastFetchedBody = System.Text.Encoding.ASCII.GetBytes("last_fetched_body");
            public static readonly byte[] GenesisLoaded = System.Text.Encoding.ASCII.GetBytes("genesis_loaded");
            // Distinct prefix from checkpoint records (cp_XXXXXXXXXXXXXXXX) so
            // SeekForPrev iteration over the cp_ range can never collide with
            // this index key, regardless of hex case conventions.
            public static readonly byte[] CheckpointLatest = System.Text.Encoding.ASCII.GetBytes("meta_last_cp");
        }
    }
}
