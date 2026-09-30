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
        void Append(FreezerIndexEntry entry);
        void TruncateToItems(long items);
        long DurableItemCount(long flushOffset);
        void Sync();
    }
}
