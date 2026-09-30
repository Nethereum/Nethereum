using System;
using System.Collections.Generic;
using System.IO;
using Microsoft.Win32.SafeHandles;

namespace Nethereum.Freezer
{
    public sealed class FreezerIndex : IFreezerIndex
    {
        private readonly SafeFileHandle _handle;
        private long _readOperationCount;

        internal long ReadOperationCount => System.Threading.Volatile.Read(ref _readOperationCount);

        private FreezerIndex(SafeFileHandle handle)
        {
            _handle = handle;
        }

        public static FreezerIndex OpenForAppend(string path)
        {
            var handle = File.OpenHandle(path, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
            return new FreezerIndex(handle);
        }

        public static FreezerIndex OpenReadOnly(string path)
        {
            var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
            return new FreezerIndex(handle);
        }

        public long Count
        {
            get
            {
                var items = RandomAccess.GetLength(_handle) / FreezerIndexEntry.Size - 1;
                return items < 0 ? 0 : items;
            }
        }

        public long ByteSize => RandomAccess.GetLength(_handle);

        public FreezerIndexEntry EntryAt(long itemNumber)
        {
            if (itemNumber < 0 || itemNumber > Count)
                throw new ArgumentOutOfRangeException(
                    nameof(itemNumber), itemNumber, $"item number must be in [0, {Count}]");

            Span<byte> buffer = stackalloc byte[FreezerIndexEntry.Size];
            ReadExact(buffer, itemNumber * FreezerIndexEntry.Size);
            return FreezerIndexEntry.ReadFrom(buffer);
        }

        public ItemRange RangeOf(long itemNumber)
        {
            var current = EntryAt(itemNumber);
            var next = EntryAt(itemNumber + 1);
            return ResolveRange(current, next);
        }

        public IReadOnlyList<FreezerIndexEntry> ReadEntries(long firstEntry, int maxEntries)
        {
            if (firstEntry < 0)
                throw new ArgumentOutOfRangeException(
                    nameof(firstEntry), firstEntry, "firstEntry must be non-negative");

            var available = Count - firstEntry + 1;
            var n = (int)Math.Min(maxEntries, Math.Max(0, available));
            if (n <= 0) return Array.Empty<FreezerIndexEntry>();

            var buffer = new byte[n * FreezerIndexEntry.Size];
            ReadExact(buffer, firstEntry * FreezerIndexEntry.Size);

            var entries = new FreezerIndexEntry[n];
            for (var i = 0; i < n; i++)
                entries[i] = FreezerIndexEntry.ReadFrom(buffer.AsSpan(i * FreezerIndexEntry.Size, FreezerIndexEntry.Size));
            return entries;
        }

        internal static ItemRange ResolveRange(FreezerIndexEntry current, FreezerIndexEntry next)
        {
            if (ItemCrossedIntoNewFile(current, next))
                return new ItemRange(next.FileNumber, start: 0, length: next.Offset);

            return new ItemRange(next.FileNumber, current.Offset, next.Offset - current.Offset);
        }

        internal static bool ItemCrossedIntoNewFile(FreezerIndexEntry current, FreezerIndexEntry next)
        {
            return current.FileNumber != next.FileNumber;
        }

        public void Append(FreezerIndexEntry entry)
        {
            Span<byte> buffer = stackalloc byte[FreezerIndexEntry.Size];
            entry.WriteTo(buffer);
            var endOfFile = RandomAccess.GetLength(_handle);
            RandomAccess.Write(_handle, buffer, endOfFile);
        }

        public void TruncateToItems(long items)
        {
            RandomAccess.SetLength(_handle, (items + 1) * FreezerIndexEntry.Size);
        }

        public long DurableItemCount(long flushOffset)
        {
            var items = flushOffset / FreezerIndexEntry.Size - 1;
            return items < 0 ? 0 : items;
        }

        private void ReadExact(Span<byte> buffer, long fileOffset)
        {
            System.Threading.Interlocked.Increment(ref _readOperationCount);
            var totalRead = 0;
            while (totalRead < buffer.Length)
            {
                var read = RandomAccess.Read(_handle, buffer.Slice(totalRead), fileOffset + totalRead);
                if (read == 0)
                    throw new EndOfStreamException("unexpected end of freezer index file");
                totalRead += read;
            }
        }

        public void Sync()
        {
            RandomAccess.FlushToDisk(_handle);
        }

        public void Dispose()
        {
            _handle.Dispose();
        }
    }
}
