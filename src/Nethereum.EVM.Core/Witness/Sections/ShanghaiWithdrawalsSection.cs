using System.Collections.Generic;
using System.IO;

namespace Nethereum.EVM.Witness.Sections
{
    public static class ShanghaiWithdrawalsSection
    {
        public static void Write(BinaryWriter w, BlockWitnessData data)
        {
            if (data.Withdrawals == null)
            {
                w.Write((byte)0);
                return;
            }

            w.Write((byte)1);
            w.Write((ushort)data.Withdrawals.Count);
            foreach (var withdrawal in data.Withdrawals)
            {
                w.Write(withdrawal.Index);
                w.Write(withdrawal.ValidatorIndex);
                BinaryWitness.WriteString(w, withdrawal.Address ?? "");
                w.Write(withdrawal.AmountInGwei);
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
            if (data[offset++] == 0)
            {
                target.Withdrawals = null;
                return;
            }

            int count = BinaryWitness.ReadU16(data, ref offset);
            var withdrawals = new List<BlockWithdrawal>(count);
            for (int i = 0; i < count; i++)
            {
                var withdrawal = new BlockWithdrawal();
                withdrawal.Index = BinaryWitness.ReadU64(data, ref offset);
                withdrawal.ValidatorIndex = BinaryWitness.ReadU64(data, ref offset);
                withdrawal.Address = BinaryWitness.ReadString(data, ref offset);
                withdrawal.AmountInGwei = BinaryWitness.ReadU64(data, ref offset);
                withdrawals.Add(withdrawal);
            }
            target.Withdrawals = withdrawals;
        }
    }
}
