using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.StateDiff
{
    public class StemDiff
    {
        public byte[] Stem { get; set; }
        public List<SuffixDiff> SuffixDiffs { get; set; } = new List<SuffixDiff>();
    }

    public class SuffixDiff
    {
        public byte SuffixIndex { get; set; }
        public byte[] OldValue { get; set; }
        public byte[] NewValue { get; set; }
    }
}
