using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.StateDiff
{
    public class BlockStateDiff
    {
        public long BlockNumber { get; set; }
        public byte[] PreStateRoot { get; set; }
        public byte[] PostStateRoot { get; set; }
        public List<StemDiff> StemDiffs { get; set; } = new List<StemDiff>();
        public List<byte[]> ProofSiblings { get; set; } = new List<byte[]>();
    }
}
