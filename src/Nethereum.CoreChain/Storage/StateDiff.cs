using System.Collections.Generic;
using System.Numerics;
using Nethereum.Model;

namespace Nethereum.CoreChain.Storage
{
    public class BlockStateDiff
    {
        public BigInteger BlockNumber { get; set; }
        public List<AccountDiffEntry> AccountDiffs { get; set; } = new();
        public List<StorageDiffEntry> StorageDiffs { get; set; } = new();
    }

    public class AccountDiffEntry
    {
        public string Address { get; set; }
        public Account PreValue { get; set; }
    }

    public class StorageDiffEntry
    {
        public string Address { get; set; }
        public BigInteger Slot { get; set; }
        public byte[] PreValue { get; set; }
    }
}
