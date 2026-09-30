using System.IO;

namespace Nethereum.EVM.Witness.Sections
{
    public static partial class WitnessSections
    {
        public static void Write(BinaryWriter w, BlockWitnessData data)
        {
            ShanghaiWithdrawalsSection.Write(w, data);
            CancunBlobSection.Write(w, data);
            PragueRequestsSection.Write(w, data);
            AmsterdamSlotSection.Write(w, data);
        }

        public static void Read(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset, BlockWitnessData target)
        {
            ShanghaiWithdrawalsSection.Read(data, ref offset, target);
            CancunBlobSection.Read(data, ref offset, target);
            PragueRequestsSection.Read(data, ref offset, target);
            AmsterdamSlotSection.Read(data, ref offset, target);
        }
    }
}
