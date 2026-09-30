using System.IO;

namespace Nethereum.EVM.Witness.Sections
{
    /// <summary>
    /// EIP-4844 blob gas accounting and the EIP-4788 parent beacon block root,
    /// Cancun onward.
    ///
    /// <para>The beacon root is the reason each of these carries its own presence
    /// byte rather than relying on a zero. An all-zero root is a legitimate VALUE:
    /// it is written to the EIP-4788 ring buffer and changes the post-state root,
    /// so a guest cannot tell "the producer sent zero" from "the producer sent
    /// nothing" by looking at the bytes. Where they were indistinguishable the
    /// guest skipped the system call entirely and still produced a proof.</para>
    /// </summary>
    public static class CancunBlobSection
    {
        public static void Write(BinaryWriter w, BlockWitnessData data)
        {
            WriteOptionalU64(w, data.BlobGasUsed);
            WriteOptionalU64(w, data.ExcessBlobGas);

            if (data.ParentBeaconBlockRoot == null)
            {
                w.Write((byte)0);
            }
            else
            {
                w.Write((byte)1);
                BinaryWitness.WriteFixedBytes(w, data.ParentBeaconBlockRoot, 32);
            }
        }

        public static void Read(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset, BlockWitnessData target)
        {
            target.BlobGasUsed = ReadOptionalU64(data, ref offset);
            target.ExcessBlobGas = ReadOptionalU64(data, ref offset);

            target.ParentBeaconBlockRoot = data[offset++] == 0
                ? null
                : BinaryWitness.ReadFixedBytes(data, ref offset, 32);
        }

        private static void WriteOptionalU64(BinaryWriter w, long? value)
        {
            if (value == null)
            {
                w.Write((byte)0);
                return;
            }
            w.Write((byte)1);
            w.Write((ulong)value.Value);
        }

        private static long? ReadOptionalU64(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset)
        {
            if (data[offset++] == 0) return null;
            return (long)BinaryWitness.ReadU64(data, ref offset);
        }
    }
}
