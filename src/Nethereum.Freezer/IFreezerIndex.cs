using System;
using System.Collections.Generic;

namespace Nethereum.Freezer
{
    public interface IFreezerIndex : IDisposable
    {
        long Count { get; }
        long ByteSize { get; }
        FreezerIndexEntry EntryAt(long itemNumber);
        IReadOnlyList<FreezerIndexEntry> ReadEntries(long firstEntry, int maxEntries);
        ItemRange RangeOf(long itemNumber);
        long? FirstItemBreakingSequence()
        {
            var previous = EntryAt(0);
            for (var item = 1L; item <= Count; item++)
            {
                var current = EntryAt(item);
                if (!FreezerIndex.IsValidSuccessor(previous, current))
                    return item;
                previous = current;
            }
            return null;
        }
        void Append(FreezerIndexEntry entry);
        void TruncateToItems(long items);
        long DurableItemCount(long flushOffset);
        void Sync();
    }
}
