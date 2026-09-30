using System.IO;

namespace Nethereum.EVM.Witness.Sections
{
    public static class AmsterdamSlotSection
    {
        public static void Write(BinaryWriter w, BlockWitnessData data)
        {
            if (data.SlotNumber == null)
            {
                w.Write((byte)0);
                return;
            }
            w.Write((byte)1);
            w.Write(data.SlotNumber.Value);
        }

        public static void Read(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset, BlockWitnessData target)
        {
            target.SlotNumber = data[offset++] == 0
                ? (ulong?)null
                : BinaryWitness.ReadU64(data, ref offset);
        }
    }
}
