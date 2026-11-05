using System;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class LargeOwner
    {
        public LargeOwner(byte[] owner, byte[] expectedStorageRoot, bool malformedBeforeSplit)
        {
            Owner = owner;
            ExpectedStorageRoot = expectedStorageRoot;
            MalformedBeforeSplit = malformedBeforeSplit;
            for (int nibble = 0; nibble < 16; nibble++) Ranges[nibble] = new NibbleRange(owner, nibble);
        }

        public byte[] Owner { get; }
        public byte[] ExpectedStorageRoot { get; }
        public bool MalformedBeforeSplit { get; }
        public NibbleRange[] Ranges { get; } = new NibbleRange[16];

        public bool Malformed => MalformedBeforeSplit || Array.Exists(Ranges, range => range.Hashed.Malformed);

        public long Slots
        {
            get
            {
                long slots = 0;
                foreach (var range in Ranges) slots += range.Hashed.Count;
                return slots;
            }
        }
    }
}
