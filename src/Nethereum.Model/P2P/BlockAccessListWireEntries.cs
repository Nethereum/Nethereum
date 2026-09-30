using System.Collections.Generic;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// EIP-8159 §BlockAccessLists (0x13): <i>"The RLP empty string (<c>0x80</c>) is
    /// returned for blocks where the BAL is unavailable."</i>
    ///
    /// <para>So a response is POSITIONAL against the request's block hashes for its
    /// whole length — entry <c>i</c> answers hash <c>i</c> — and an unavailable list
    /// occupies its position rather than ending the response. This is where the two
    /// protocols that carry an EIP-7928 list, eth/71 and snap/2, are made to agree:
    /// they have separate message types but one wire rule, and each having its own
    /// copy of it is how they came to disagree.</para>
    ///
    /// <para>Not stop-at-gap. geth serves a prefix and stops at the first hash it
    /// cannot answer; the EIP does not, and geth is a reference rather than the
    /// specification. A serve path that stops at a gap and a reader that indexes by
    /// position hand back a DIFFERENT block's access list, which then fails against
    /// <c>header.BlockAccessListHash</c> for a block that was served correctly.</para>
    /// </summary>
    public static class BlockAccessListWireEntries
    {
        private static readonly byte[] RlpEmptyString = { 0x80 };

        public static byte[][] ForWire(IList<byte[]> blockAccessListsByBlock)
        {
            if (blockAccessListsByBlock == null) return new byte[0][];

            var entries = new byte[blockAccessListsByBlock.Count][];
            for (var i = 0; i < blockAccessListsByBlock.Count; i++)
            {
                var entry = blockAccessListsByBlock[i];
                entries[i] = (entry == null || entry.Length == 0) ? RlpEmptyString : entry;
            }
            return entries;
        }
    }
}
