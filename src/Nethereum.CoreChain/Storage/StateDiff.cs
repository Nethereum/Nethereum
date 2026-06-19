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

        /// <summary>
        /// Canonical storage-trie path: <c>keccak256(padded slot bytes)</c>
        /// (Yellow Paper §4.1). Stored as the raw 32-byte hash; the original
        /// <see cref="BigInteger"/> slot is not recoverable from the persistent
        /// diff store. Block-execution writes hash on capture; rewind applies
        /// via the same hashed key.
        /// </summary>
        public byte[] SlotKey { get; set; }
        public byte[] PreValue { get; set; }
    }
}
