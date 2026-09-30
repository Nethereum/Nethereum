using Nethereum.Documentation;
using System.Collections.Generic;

namespace Nethereum.Merkle.Binary.StateDiff
{
    [NethereumDocExample(DocSection.ChainInfrastructure, "binary-trie-state-diff", "The changes a block made under one stem")]
    public class StemDiff
    {
        public byte[] Stem { get; set; }
        public List<SuffixDiff> SuffixDiffs { get; set; } = new List<SuffixDiff>();
    }

    [NethereumDocExample(DocSection.ChainInfrastructure, "binary-trie-state-diff", "One sub-index value change inside a stem")]
    public class SuffixDiff
    {
        public byte SuffixIndex { get; set; }
        public byte[] OldValue { get; set; }
        public byte[] NewValue { get; set; }
    }
}
