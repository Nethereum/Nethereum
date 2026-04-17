using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.StateDiff
{
    public class BinaryTrieStateDiff
    {
        public const byte VERSION = 1;

        public byte Version { get; set; } = VERSION;
        public long BlockNumber { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] PostStateRoot { get; set; }
        public List<StemDiff> StemDiffs { get; set; } = new List<StemDiff>();
        public List<byte[]> ProofSiblings { get; set; } = new List<byte[]>();
    }
}
