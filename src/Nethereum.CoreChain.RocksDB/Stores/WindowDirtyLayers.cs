using System;
using System.Collections.Generic;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Util;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    public sealed class WindowDirtyLayers
    {
        private readonly object _gate = new object();

        private static readonly byte[] Tombstone = new byte[0];

        internal static byte[] Key(bool isAccount, byte[] owner, byte[] path)
        {
            var ownerLen = isAccount ? 0 : (owner?.Length ?? 0);
            var pathLen = path?.Length ?? 0;
            var key = new byte[1 + ownerLen + pathLen];
            key[0] = isAccount ? (byte)0x00 : (byte)0x01;
            if (ownerLen > 0) Buffer.BlockCopy(owner, 0, key, 1, ownerLen);
            if (pathLen > 0) Buffer.BlockCopy(path, 0, key, 1 + ownerLen, pathLen);
            return key;
        }

        private readonly SortedList<ulong, Dictionary<byte[], byte[]>> _layers = new SortedList<ulong, Dictionary<byte[], byte[]>>();

        private readonly Dictionary<byte[], List<ulong>> _ownerWipes = new Dictionary<byte[], List<ulong>>(ByteArrayComparer.Current);

        public void PushOwnerWipe(ulong block, byte[] owner)
        {
            if (owner == null || owner.Length == 0) return;
            lock (_gate)
            {
                if (!_ownerWipes.TryGetValue(owner, out var blocks))
                {
                    blocks = new List<ulong>();
                    _ownerWipes[owner] = blocks;
                }
                var idx = blocks.BinarySearch(block);
                if (idx < 0) blocks.Insert(~idx, block);
            }
        }

        private ulong? GetWipeBarrier(byte[] owner, ulong forBlock)
        {
            if (owner == null || owner.Length == 0) return null;
            if (!_ownerWipes.TryGetValue(owner, out var blocks) || blocks.Count == 0) return null;

            int lo = 0, hi = blocks.Count - 1, hit = -1;
            while (lo <= hi)
            {
                int mid = (lo + hi) / 2;
                if (blocks[mid] < forBlock) { hit = mid; lo = mid + 1; }
                else hi = mid - 1;
            }
            return hit >= 0 ? blocks[hit] : (ulong?)null;
        }

        public bool TryGetOwnerWipeBarrier(byte[] owner, ulong belowBlock, out ulong wipeBlock)
        {
            lock (_gate)
            {
                var wb = GetWipeBarrier(owner, belowBlock);
                if (wb.HasValue) { wipeBlock = wb.Value; return true; }
                wipeBlock = 0;
                return false;
            }
        }

        public void PushLayer(ulong block, TrieNodeSet nodes)
        {
            if (nodes == null) return;

            var layer = new Dictionary<byte[], byte[]>(ByteArrayComparer.Current);

            foreach (var node in nodes.Nodes)
            {
                var isAccount = node.Owner == null || node.Owner.Length == 0;
                var key = Key(isAccount, node.Owner, node.Path);
                layer[key] = node.GetEncodedData() ?? Array.Empty<byte>();
            }

            foreach (var delete in nodes.Deletes)
            {
                var isAccount = delete.Owner == null || delete.Owner.Length == 0;
                var key = Key(isAccount, delete.Owner, delete.Path);
                layer[key] = Tombstone;
            }

            lock (_gate)
            {
                PushCount++;
                _layers[block] = layer;
            }
        }

        public bool TryGetPreImage(bool isAccount, byte[] owner, byte[] path, ulong forBlock, out byte[] preImage, out bool isAbsent)
        {
            lock (_gate)
            {
                LookupCount++;
                var key = Key(isAccount, owner, path);
                var wipeBarrier = isAccount ? null : GetWipeBarrier(owner, forBlock);
                var blocks = _layers.Keys;
                var layers = _layers.Values;

                for (int i = blocks.Count - 1; i >= 0; i--)
                {
                    var block = blocks[i];
                    if (block >= forBlock) continue;

                    if (layers[i].TryGetValue(key, out var value))
                    {
                        if (wipeBarrier.HasValue && block < wipeBarrier.Value)
                        {
                            preImage = null;
                            isAbsent = true;
                            return true;
                        }

                        if (ReferenceEquals(value, Tombstone))
                        {
                            preImage = null;
                            isAbsent = true;
                        }
                        else
                        {
                            preImage = value;
                            isAbsent = false;
                        }
                        return true;
                    }
                }

                if (wipeBarrier.HasValue)
                {
                    preImage = null;
                    isAbsent = true;
                    return true;
                }

                preImage = null;
                isAbsent = false;
                return false;
            }
        }

        public bool TryGetLatest(bool isAccount, byte[] owner, byte[] path, out byte[] value, out bool isAbsent)
        {
            lock (_gate)
            {
                LookupCount++;
                var key = Key(isAccount, owner, path);
                var wipeBarrier = isAccount ? null : GetWipeBarrier(owner, ulong.MaxValue);
                var blocks = _layers.Keys;
                var layers = _layers.Values;

                for (int i = blocks.Count - 1; i >= 0; i--)
                {
                    var block = blocks[i];

                    if (layers[i].TryGetValue(key, out var found))
                    {
                        if (wipeBarrier.HasValue && block < wipeBarrier.Value)
                        {
                            value = null;
                            isAbsent = true;
                            return true;
                        }

                        if (ReferenceEquals(found, Tombstone))
                        {
                            value = null;
                            isAbsent = true;
                        }
                        else
                        {
                            value = found;
                            isAbsent = false;
                        }
                        return true;
                    }
                }

                if (wipeBarrier.HasValue)
                {
                    value = null;
                    isAbsent = true;
                    return true;
                }

                value = null;
                isAbsent = false;
                return false;
            }
        }

        public System.Collections.Generic.Dictionary<byte[], (byte[] Value, bool IsTombstone)> EnumerateOwner(byte[] owner, ulong belowBlock)
        {
            var result = new System.Collections.Generic.Dictionary<byte[], (byte[] Value, bool IsTombstone)>(ByteArrayComparer.Current);
            if (owner == null || owner.Length == 0) return result;

            lock (_gate)
            {
                var wipeBarrier = GetWipeBarrier(owner, belowBlock);

                var prefix = Key(isAccount: false, owner, path: Array.Empty<byte>());
                var blocks = _layers.Keys;
                var layers = _layers.Values;

                for (int i = blocks.Count - 1; i >= 0; i--)
                {
                    var block = blocks[i];
                    if (block >= belowBlock) continue;
                    if (wipeBarrier.HasValue && block < wipeBarrier.Value) break;

                    foreach (var kv in layers[i])
                    {
                        var key = kv.Key;
                        if (!StartsWithPrefix(key, prefix)) continue;
                        var path = new byte[key.Length - prefix.Length];
                        Buffer.BlockCopy(key, prefix.Length, path, 0, path.Length);
                        if (result.ContainsKey(path)) continue;

                        if (ReferenceEquals(kv.Value, Tombstone))
                            result[path] = (null, true);
                        else
                            result[path] = (kv.Value, false);
                    }
                }

                return result;
            }
        }

        private static bool StartsWithPrefix(byte[] value, byte[] prefix)
        {
            if (value.Length < prefix.Length) return false;
            for (int i = 0; i < prefix.Length; i++)
                if (value[i] != prefix[i]) return false;
            return true;
        }

        public void DropAbove(ulong block)
        {
            lock (_gate)
            {
                for (int i = _layers.Count - 1; i >= 0; i--)
                {
                    if (_layers.Keys[i] > block) _layers.RemoveAt(i);
                }
                DropOwnerWipes(b => b > block);
            }
        }

        public void DropThrough(ulong block)
        {
            lock (_gate)
            {
                for (int i = _layers.Count - 1; i >= 0; i--)
                {
                    if (_layers.Keys[i] <= block) _layers.RemoveAt(i);
                }
                DropOwnerWipes(b => b <= block);
            }
        }

        private void DropOwnerWipes(Func<ulong, bool> shouldRemove)
        {
            if (_ownerWipes.Count == 0) return;
            List<byte[]> emptied = null;
            foreach (var kv in _ownerWipes)
            {
                kv.Value.RemoveAll(b => shouldRemove(b));
                if (kv.Value.Count == 0) (emptied ??= new List<byte[]>()).Add(kv.Key);
            }
            if (emptied != null)
                foreach (var owner in emptied) _ownerWipes.Remove(owner);
        }

        public int LayerCount { get { lock (_gate) { return _layers.Count; } } }

        public long LookupCount { get; private set; }

        public long PushCount { get; private set; }

        public long ApproxBytes
        {
            get
            {
                lock (_gate)
                {
                    long total = 0;
                    foreach (var layer in _layers.Values)
                    {
                        foreach (var kv in layer)
                            total += kv.Key.Length + kv.Value.Length;
                    }
                    return total;
                }
            }
        }
    }
}
