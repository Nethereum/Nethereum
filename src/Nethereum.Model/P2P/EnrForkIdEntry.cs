using Nethereum.RLP;

namespace Nethereum.Model.P2P
{
    /// <summary>
    /// Codec for the ENR "eth" key value. EIP-2124 defines only the fork-id pair itself
    /// (<c>[forkHash, forkNext]</c>, see <see cref="ForkIdEncoder"/>) and does not name an ENR key;
    /// the "eth" key and its shape come from go-ethereum's <c>eth/protocols/eth</c> <c>enrEntry</c>:
    /// <c>[ForkID, ...tail]</c>. The "eth" value is therefore the NESTED list
    /// <c>[[forkHash, forkNext]]</c> — the EIP-2124 pair wrapped in a one-element outer list, with
    /// room after it for a forward-compat tail geth may add. This is deliberately NOT the flat
    /// <c>[forkHash, forkNext]</c> pair Status messages carry on the wire.
    /// </summary>
    public static class EnrForkIdEntry
    {
        public static byte[] Encode(uint forkHash, ulong forkNext)
        {
            var forkId = ForkIdEncoder.Encode(forkHash, forkNext);
            return RLP.RLP.EncodeList(forkId);
        }

        public static void Decode(byte[] ethEntryValue, out uint forkHash, out ulong forkNext)
        {
            var outer = (RLPCollection)RLP.RLP.Decode(ethEntryValue);
            var forkIdItem = (RLPCollection)outer[0];
            ForkIdEncoder.DecodeItems(forkIdItem, out forkHash, out forkNext);
        }
    }
}
