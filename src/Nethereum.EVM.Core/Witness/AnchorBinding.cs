using Nethereum.Documentation;
using System;
using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.EVM.Witness
{
    [NethereumDocExample(DocSection.EvmSimulator, "witness", "Encode and commit to anchor words carried by a witness")]
    public static class AnchorBinding
    {
        public const int WordSize = 32;
        public const int CommitmentSlots = 8;

        public static byte[] Encode(IReadOnlyList<EvmUInt256> words)
        {
            if (words == null) throw new ArgumentNullException(nameof(words));
            var encoded = new byte[words.Count * WordSize];
            for (int i = 0; i < words.Count; i++)
                Array.Copy(words[i].ToBigEndian(), 0, encoded, i * WordSize, WordSize);
            return encoded;
        }

        public static byte[] Commitment(byte[] anchorInputs)
        {
            if (anchorInputs == null) throw new ArgumentNullException(nameof(anchorInputs));
            return new Sha3Keccack().CalculateHash(anchorInputs);
        }
    }
}
