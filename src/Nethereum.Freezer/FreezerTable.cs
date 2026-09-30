using System;
using System.Collections.Generic;
using System.IO;

namespace Nethereum.Freezer
{
    public sealed class FreezerTable<T> : IDisposable
    {
        public string Name { get; }
        public long Count => _index.Count;
        public long VirtualTail { get; private set; }

        public long DurableHead => VirtualTail + _index.DurableItemCount(_flushOffset);

        private readonly IFreezerIndex _index;
        private readonly IFreezerDataFiles _data;
        private readonly IItemCodec<T> _codec;
        private readonly string _metaPath;
        private long _flushOffset;

        private FreezerTable(string name, IFreezerIndex index, IFreezerDataFiles data, IItemCodec<T> codec,
            string metaPath, long virtualTail)
        {
            Name = name;
            _index = index;
            _data = data;
            _codec = codec;
            _metaPath = metaPath;
            VirtualTail = virtualTail;
        }

        public static FreezerTable<T> OpenReadOnly(FreezerTablePaths paths, IItemCodec<T> codec)
        {
            var index = FreezerIndex.OpenReadOnly(paths.IndexPath);
            var data = OrDisposeOnFailure(index, () =>
                RollingDataFiles.OpenReadOnly(paths.Directory, paths.Name, paths.UseCompression));
            var table = new FreezerTable<T>(paths.Name, index, data, codec, paths.MetaPath, virtualTail: 0);

            try
            {
                table.Validate(ReadMeta(paths.MetaPath, index));
            }
            catch
            {
                table.Dispose();
                throw;
            }

            return table;
        }

        public static FreezerTable<T> OpenForAppend(FreezerTablePaths paths, IItemCodec<T> codec, long maxFileSize)
        {
            Directory.CreateDirectory(paths.Directory);

            var index = FreezerIndex.OpenForAppend(paths.IndexPath);
            DisposeOnFailure(index, () => BootstrapSentinelIfEmpty(index));

            var data = OrDisposeOnFailure(index, () =>
                RollingDataFiles.OpenForAppend(paths.Directory, paths.Name, paths.UseCompression, (uint)maxFileSize));
            var table = new FreezerTable<T>(paths.Name, index, data, codec, paths.MetaPath, virtualTail: 0);

            try
            {
                table.Repair(ReadOrCreateMeta(paths.MetaPath, index));
            }
            catch
            {
                table.Dispose();
                throw;
            }

            return table;
        }

        private static void DisposeOnFailure(IDisposable indexGuard, Action action)
        {
            try
            {
                action();
            }
            catch
            {
                indexGuard.Dispose();
                throw;
            }
        }

        private static TResult OrDisposeOnFailure<TResult>(IDisposable indexGuard, Func<TResult> action)
        {
            try
            {
                return action();
            }
            catch
            {
                indexGuard.Dispose();
                throw;
            }
        }

        private static void BootstrapSentinelIfEmpty(IFreezerIndex index)
        {
            if (index.ByteSize == 0)
                index.Append(new FreezerIndexEntry(0, 0));
        }

        private static FreezerTableMeta ReadMeta(string path, IFreezerIndex index)
        {
            if (!File.Exists(path))
                return new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail: 0, index.ByteSize);

            return FreezerTableMeta.Decode(File.ReadAllBytes(path));
        }

        private static FreezerTableMeta ReadOrCreateMeta(string path, IFreezerIndex index)
        {
            if (File.Exists(path))
                return FreezerTableMeta.Decode(File.ReadAllBytes(path));

            var fresh = new FreezerTableMeta(FreezerTableMeta.SupportedVersion, virtualTail: 0, index.ByteSize);
            fresh.WriteAtomic(path);
            return fresh;
        }

        public T Read(long itemNumber)
        {
            var range = _index.RangeOf(itemNumber);
            var raw = _data.Read(range.FileNumber, range.Start, range.Length);
            return _codec.Decode(raw);
        }

        public IReadOnlyList<(long ItemNumber, byte[] Raw)> ReadSealedSegment(ushort fileNumber)
        {
            if (fileNumber >= _data.CurrentFileNumber)
                throw new FreezerConsistencyException(
                    $"freezer table '{Name}' file {fileNumber} is not sealed (current file {_data.CurrentFileNumber}); bulk segment read is for sealed files only");

            var sealedCount = SealedPhysicalItemCount();
            var result = new List<(long, byte[])>();
            var cursor = FirstItemInFile(fileNumber, Count);
            while (cursor < sealedCount && _index.RangeOf(cursor).FileNumber == fileNumber)
            {
                var (items, next) = ReadSealedChunk(cursor, ChunkReadBytes, sealedCount);
                if (items.Count == 0) break;
                result.AddRange(items);
                cursor = next;
            }
            return result;
        }

        public IReadOnlyList<(long ItemNumber, byte[] Raw)> ReadSealedRange(long startItem, int maxItems)
            => ReadSealedRange(startItem, maxItems, SealedPhysicalItemCount());

        public IReadOnlyList<(long ItemNumber, byte[] Raw)> ReadSealedRange(long startItem, int maxItems, long endExclusive)
        {
            if (startItem < 0)
                throw new FreezerConsistencyException(
                    $"freezer table '{Name}' ReadSealedRange startItem {startItem} is negative");
            if (maxItems < 0)
                throw new FreezerConsistencyException(
                    $"freezer table '{Name}' ReadSealedRange maxItems {maxItems} is negative");

            var end = Math.Min(startItem + maxItems, endExclusive);
            var result = new List<(long, byte[])>();
            var cursor = startItem;
            while (cursor < end)
            {
                var (items, next) = ReadSealedChunk(cursor, ChunkReadBytes, end);
                if (items.Count == 0) break;
                result.AddRange(items);
                cursor = next;
            }
            return result;
        }

        private const int ChunkReadBytes = 256 * 1024 * 1024;
        private const int IndexSlabEntries = 1024 * 1024 / FreezerIndexEntry.Size;

        public (IReadOnlyList<(long ItemNumber, byte[] Raw)> Items, long NextItem) ReadSealedChunk(
            long startItem, int targetBytes, long endExclusive)
        {
            if (targetBytes <= 0)
                throw new FreezerConsistencyException(
                    $"freezer table '{Name}' ReadSealedChunk targetBytes {targetBytes} must be positive");

            var limit = Math.Min(endExclusive, Count);
            if (startItem < 0 || startItem >= limit)
                return (System.Array.Empty<(long, byte[])>(), startItem);

            var ranges = ResolveChunkRanges(startItem, limit, (uint)targetBytes);

            var first = ranges[0];
            var fileNumber = first.FileNumber;
            var spanStart = first.Start;
            var lastRange = ranges[ranges.Count - 1];
            var spanLength = (lastRange.Start - spanStart) + lastRange.Length;
            var buffer = _data.Read(fileNumber, spanStart, spanLength);

            var items = new List<(long, byte[])>(ranges.Count);
            for (var k = 0; k < ranges.Count; k++)
            {
                var r = ranges[k];
                var slice = new byte[r.Length];
                System.Array.Copy(buffer, (int)(r.Start - spanStart), slice, 0, (int)r.Length);
                items.Add((VirtualTail + startItem + k, slice));
            }
            return (items, startItem + ranges.Count);
        }

        private List<ItemRange> ResolveChunkRanges(long startItem, long limit, uint targetBytes)
        {
            var ranges = new List<ItemRange>();
            var entryBase = startItem;
            var entries = _index.ReadEntries(entryBase, IndexSlabEntries + 1);
            var offset = 0;
            var fileNumber = default(ushort);
            long acc = 0;

            for (var item = startItem; item < limit; item++)
            {
                if (offset + 1 >= entries.Count)
                {
                    entryBase += offset;
                    entries = _index.ReadEntries(entryBase, IndexSlabEntries + 1);
                    offset = 0;
                }

                var range = FreezerIndex.ResolveRange(entries[offset], entries[offset + 1]);
                offset++;

                if (ranges.Count == 0)
                {
                    fileNumber = range.FileNumber;
                }
                else
                {
                    if (range.FileNumber != fileNumber) break;
                    if (acc + range.Length > targetBytes) break;
                }

                ranges.Add(range);
                acc += range.Length;
            }

            return ranges;
        }

        public ushort FileNumberOf(long itemNumber) => _index.RangeOf(itemNumber).FileNumber;

        internal long IndexReadOperationCount => (_index as FreezerIndex)?.ReadOperationCount ?? 0;

        private long FirstItemInFile(ushort fileNumber, long count)
        {
            var lo = 0L;
            var hi = count;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (_index.RangeOf(mid).FileNumber >= fileNumber) hi = mid;
                else lo = mid + 1;
            }
            return lo;
        }

        public void Append(long expectedItemNumber, T item) => AppendEncoded(expectedItemNumber, _codec.Encode(item));

        public byte[] Encode(T item) => _codec.Encode(item);

        public T Decode(ReadOnlySpan<byte> raw) => _codec.Decode(raw);

        public void AppendEncoded(long expectedItemNumber, byte[] encoded)
        {
            if (expectedItemNumber != Count)
                throw new FreezerConsistencyException(
                    $"freezer table '{Name}' expected next item {Count}, got {expectedItemNumber}");

            var (fileNumber, offset) = _data.Append(encoded);
            _index.Append(new FreezerIndexEntry(fileNumber, offset + (uint)encoded.Length));
        }

        public void SyncIndex() => _index.Sync();

        public void SyncData() => _data.SyncCurrent();

        public void PersistMeta() => WriteMeta(_index.ByteSize);

        public long SealedHead => VirtualTail + SealedPhysicalItemCount();

        private long SealedPhysicalItemCount()
        {
            if (Count == 0) return 0;

            var currentFile = _data.CurrentFileNumber;
            if (_index.EntryAt(Count).FileNumber < currentFile) return Count;

            var lo = 1L;
            var hi = Count;
            while (lo < hi)
            {
                var mid = lo + (hi - lo) / 2;
                if (_index.EntryAt(mid).FileNumber >= currentFile) hi = mid;
                else lo = mid + 1;
            }
            return lo - 1;
        }

        internal void RaiseVirtualTail(long newVirtualTail)
        {
            if (newVirtualTail < VirtualTail)
                throw new FreezerConsistencyException(
                    $"freezer table '{Name}' cannot lower VirtualTail from {VirtualTail} to {newVirtualTail}");

            VirtualTail = newVirtualTail;
            WriteMeta(_index.ByteSize);
        }

        public long TruncateHead(long newItemCount)
        {
            if (newItemCount < 0)
                throw new FreezerImmutableException(
                    $"cannot truncate freezer table '{Name}' to physical item {newItemCount}: below its tail (VirtualTail {VirtualTail})");

            if (newItemCount >= Count)
                return Count;

            var flushOffset = (newItemCount + 1) * FreezerIndexEntry.Size;
            WriteMeta(flushOffset);

            _index.TruncateToItems(newItemCount);
            var finalEntry = _index.EntryAt(newItemCount);
            _data.TruncateHeadTo(finalEntry.FileNumber, finalEntry.Offset);

            WriteMeta(flushOffset);
            return newItemCount;
        }

        private void Repair(FreezerTableMeta meta)
        {
            RepairIndexSelfConsistency(throwOnDefect: false);

            var flushOffset = ReconcileIndexAgainstFlushOffset(meta.FlushOffset, throwOnDefect: false);
            flushOffset = ReconcileIndexAgainstData(flushOffset, throwOnDefect: false);

            VirtualTail = meta.VirtualTail;
            WriteMeta(flushOffset);
        }

        private void Validate(FreezerTableMeta meta)
        {
            RepairIndexSelfConsistency(throwOnDefect: true);
            ReconcileIndexAgainstFlushOffset(meta.FlushOffset, throwOnDefect: true);
            ReconcileIndexAgainstData(flushOffset: 0, throwOnDefect: true);

            VirtualTail = meta.VirtualTail;
            _flushOffset = meta.FlushOffset;

            var expectedByteSize = (_index.Count + 1) * FreezerIndexEntry.Size;
            if (_index.ByteSize != expectedByteSize)
                throw new FreezerValidationException(
                    $"freezer index '{Name}' byte length {_index.ByteSize} != expected {expectedByteSize}");
        }

        private void RepairIndexSelfConsistency(bool throwOnDefect)
        {
            for (var item = 1L; item <= _index.Count; item++)
            {
                var previous = _index.EntryAt(item - 1);
                var current = _index.EntryAt(item);
                if (IsValidSuccessor(previous, current))
                    continue;

                if (throwOnDefect)
                    throw new FreezerValidationException(
                        $"freezer index '{Name}' has a monotonicity violation at item {item - 1}");

                _index.TruncateToItems(item - 1);
                return;
            }
        }

        private static bool IsValidSuccessor(FreezerIndexEntry previous, FreezerIndexEntry current)
        {
            if (current.FileNumber == previous.FileNumber)
                return current.Offset >= previous.Offset;

            return current.FileNumber == previous.FileNumber + 1;
        }

        private long ReconcileIndexAgainstFlushOffset(long flushOffset, bool throwOnDefect)
        {
            var flushOffsetItems = _index.DurableItemCount(flushOffset);

            if (_index.Count > flushOffsetItems)
            {
                if (throwOnDefect)
                    throw new FreezerValidationException(
                        $"freezer index '{Name}' ({_index.Count} items) is ahead of flushOffset ({flushOffsetItems} items)");

                _index.TruncateToItems(flushOffsetItems);
                return flushOffset;
            }

            if (_index.Count < flushOffsetItems)
                return (_index.Count + 1) * FreezerIndexEntry.Size;

            return flushOffset;
        }

        private long ReconcileIndexAgainstData(long flushOffset, bool throwOnDefect)
        {
            while (true)
            {
                var tail = _index.EntryAt(_index.Count);
                var actualLength = _data.LengthOf(tail.FileNumber);

                if (tail.Offset == actualLength)
                    return flushOffset;

                if (throwOnDefect)
                    throw new FreezerValidationException(
                        $"freezer data file for '{Name}' (file {tail.FileNumber}, {actualLength} bytes) " +
                        $"does not match index tail (item {_index.Count}, offset {tail.Offset})");

                if (tail.Offset < actualLength)
                {
                    _data.TruncateHeadTo(tail.FileNumber, tail.Offset);
                    return flushOffset;
                }

                _index.TruncateToItems(_index.Count - 1);
                flushOffset = _index.ByteSize;
            }
        }

        private void WriteMeta(long flushOffset)
        {
            _flushOffset = flushOffset;
            var meta = new FreezerTableMeta(FreezerTableMeta.SupportedVersion, VirtualTail, flushOffset);
            meta.WriteAtomic(_metaPath);
        }

        public void Dispose()
        {
            _index.Dispose();
            _data.Dispose();
        }
    }
}
