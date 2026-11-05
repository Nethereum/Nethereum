using System;
using System.Collections.Generic;
using Nethereum.CoreChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Engine
{
    public sealed class EnginePayloadStash
    {
        private const int HeaderOverheadBytes = 1024;
        private const int WithdrawalBytes = 64;
        private const int RetainedBytesPerEncodedTransactionByte = 4;

        private sealed class Entry
        {
            public Entry(BlockBundle block, long bytes)
            {
                Block = block;
                Bytes = bytes;
            }

            public BlockBundle Block { get; }
            public long Bytes { get; }
            public LinkedListNode<string> Position { get; set; }
        }

        private readonly object _gate = new object();
        private readonly Dictionary<string, Entry> _blocks = new Dictionary<string, Entry>();
        private readonly LinkedList<string> _order = new LinkedList<string>();
        private readonly int _maxBlocks;
        private readonly long _maxBytes;
        private long _retainedBytes;

        public EnginePayloadStash() : this(new EnginePayloadStashOptions())
        {
        }

        public EnginePayloadStash(EnginePayloadStashOptions options)
        {
            if (options == null) throw new ArgumentNullException(nameof(options));
            if (options.MaxBlocks < 1) throw new ArgumentOutOfRangeException(nameof(options), "MaxBlocks must be at least 1");
            if (options.MaxBytes < 1) throw new ArgumentOutOfRangeException(nameof(options), "MaxBytes must be at least 1");
            _maxBlocks = options.MaxBlocks;
            _maxBytes = options.MaxBytes;
        }

        public int Count
        {
            get { lock (_gate) return _blocks.Count; }
        }

        public long RetainedBytes
        {
            get { lock (_gate) return _retainedBytes; }
        }

        public void Put(BlockBundle block)
        {
            var key = block.HeaderHash.ToHex();
            var entry = new Entry(block, RetainedBytesOf(block));
            lock (_gate)
            {
                Evict(key);
                entry.Position = _order.AddLast(key);
                _blocks[key] = entry;
                _retainedBytes += entry.Bytes;

                while (_order.Count > 1 && (_blocks.Count > _maxBlocks || _retainedBytes > _maxBytes))
                    Evict(_order.First.Value);
            }
        }

        public bool TryGet(byte[] hash, out BlockBundle block)
        {
            lock (_gate)
            {
                if (_blocks.TryGetValue(hash.ToHex(), out var entry))
                {
                    block = entry.Block;
                    return true;
                }
                block = null;
                return false;
            }
        }

        public bool Contains(byte[] hash)
        {
            lock (_gate) return _blocks.ContainsKey(hash.ToHex());
        }

        public void Remove(byte[] hash)
        {
            lock (_gate) Evict(hash.ToHex());
        }

        public void PruneBelow(ulong blockNumber)
        {
            lock (_gate)
            {
                var stale = new List<string>();
                foreach (var entry in _blocks)
                {
                    if ((ulong)entry.Value.Block.Header.BlockNumber < blockNumber) stale.Add(entry.Key);
                }
                foreach (var key in stale) Evict(key);
            }
        }

        private void Evict(string key)
        {
            if (!_blocks.TryGetValue(key, out var entry)) return;
            _blocks.Remove(key);
            _order.Remove(entry.Position);
            _retainedBytes -= entry.Bytes;
        }

        private static long RetainedBytesOf(BlockBundle block)
        {
            long bytes = HeaderOverheadBytes + (block.Header.ExtraData?.Length ?? 0);
            foreach (var transaction in block.Transactions)
                bytes += (long)transaction.GetRLPEncoded().Length * RetainedBytesPerEncodedTransactionByte;
            bytes += (long)(block.Withdrawals?.Count ?? 0) * WithdrawalBytes;
            return bytes;
        }
    }
}
