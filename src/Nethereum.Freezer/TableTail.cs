using System.Collections.Generic;

namespace Nethereum.Freezer
{
    public readonly struct TableTail
    {
        public long VirtualTail { get; }
        public IReadOnlyList<string> Members { get; }

        public TableTail(long virtualTail, IReadOnlyList<string> members)
        {
            VirtualTail = virtualTail;
            Members = members;
        }
    }
}
