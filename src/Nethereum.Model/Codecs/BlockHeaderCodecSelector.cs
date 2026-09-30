using System;

namespace Nethereum.Model.Codecs
{
    public static class BlockHeaderCodecSelector
    {
        public static IBlockHeaderCodec ForHeader(BlockHeader header)
        {
            if (header.BaseFee == null) return LegacyBlockHeaderCodec.Instance;
            if (header.WithdrawalsRoot == null) return LondonBlockHeaderCodec.Instance;
            if (header.ParentBeaconBlockRoot == null) return ShanghaiBlockHeaderCodec.Instance;
            if (header.RequestsHash == null) return CancunBlockHeaderCodec.Instance;

            // EIP-7928 and EIP-7843 are emitted together or not at all, so
            // neither present is a Prague header and both present is an
            // Amsterdam one. Exactly one present is neither shape, and no
            // fork produces it, so it is a header something built wrongly.
            //
            // It is rejected rather than encoded. Encoding it as Prague drops
            // the field that IS set and yields a hash that looks valid while
            // committing to less than the header carries -- which a producer
            // and an importer would then disagree about, silently.
            //
            // Both absent stays permissive on purpose: that shape is
            // bit-for-bit a legitimate pre-Amsterdam header, so nothing here
            // can tell it from an Amsterdam header nobody populated. The
            // callers that know their fork bypass this method and let the
            // fork's own codec throw.
            var hasAccessListHash = header.BlockAccessListHash != null;
            var hasSlotNumber = header.SlotNumber != null;
            if (hasAccessListHash != hasSlotNumber)
                throw new ArgumentException(
                    "Header carries " +
                    (hasAccessListHash
                        ? "a block access list hash without a slot number"
                        : "a slot number without a block access list hash") +
                    ". The EIP-7928 and EIP-7843 fields are emitted together or not at all, " +
                    "so no fork produces this header.", nameof(header));

            if (!hasAccessListHash)
                return PragueBlockHeaderCodec.Instance;

            return AmsterdamBlockHeaderCodec.Instance;
        }

        public static IBlockHeaderCodec ForFieldCount(int count)
        {
            switch (count)
            {
                case 15: return LegacyBlockHeaderCodec.Instance;
                case 16: return LondonBlockHeaderCodec.Instance;
                case 17: return ShanghaiBlockHeaderCodec.Instance;
                case 20: return CancunBlockHeaderCodec.Instance;
                case 21: return PragueBlockHeaderCodec.Instance;
                case 23: return AmsterdamBlockHeaderCodec.Instance;
                default: return null;
            }
        }
    }
}
