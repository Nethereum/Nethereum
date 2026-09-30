using System.IO;

namespace Nethereum.EVM.Witness.Sections
{
    public static class PragueRequestsSection
    {
        public static void Write(BinaryWriter w, BlockWitnessData data)
        {
            if (data.RequestsHash == null)
            {
                w.Write((byte)0);
                return;
            }
            w.Write((byte)1);
            BinaryWitness.WriteFixedBytes(w, data.RequestsHash, 32);
        }

        public static void Read(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset, BlockWitnessData target)
        {
            target.RequestsHash = data[offset++] == 0
                ? null
                : BinaryWitness.ReadFixedBytes(data, ref offset, 32);
        }
    }
}
