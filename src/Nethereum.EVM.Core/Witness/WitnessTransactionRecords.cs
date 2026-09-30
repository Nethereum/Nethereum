using System.Collections.Generic;
using System.IO;

namespace Nethereum.EVM.Witness
{
    internal static class WitnessTransactionRecords
    {
        private const byte Absent = 0;
        private const byte Present = 1;

        internal static void Write(BinaryWriter w, List<BlockWitnessTransaction> transactions)
        {
            var txs = transactions ?? new List<BlockWitnessTransaction>();
            w.Write((ushort)txs.Count);
            foreach (var tx in txs)
            {
                BinaryWitness.WriteString(w, tx.From ?? "");
                BinaryWitness.WriteBytes(w, tx.RlpEncoded ?? new byte[0]);
                WriteAuthorities(w, tx.AuthorisationAuthorities);
            }
        }

        internal static List<BlockWitnessTransaction> Read(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset)
        {
            int count = BinaryWitness.ReadU16(data, ref offset);
            var transactions = new List<BlockWitnessTransaction>(count);
            for (int i = 0; i < count; i++)
            {
                var tx = new BlockWitnessTransaction();
                tx.From = BinaryWitness.ReadString(data, ref offset);
                tx.RlpEncoded = BinaryWitness.ReadBytesArray(data, ref offset);
                tx.AuthorisationAuthorities = ReadAuthorities(data, ref offset);
                transactions.Add(tx);
            }
            return transactions;
        }

        private static void WriteAuthorities(BinaryWriter w, List<string> authorities)
        {
            if (authorities == null)
            {
                w.Write(Absent);
                return;
            }

            w.Write(Present);
            w.Write((ushort)authorities.Count);
            foreach (var authority in authorities)
            {
                if (string.IsNullOrEmpty(authority))
                {
                    w.Write(Absent);
                    continue;
                }
                w.Write(Present);
                BinaryWitness.WriteString(w, authority);
            }
        }

        private static List<string> ReadAuthorities(
#if NETCOREAPP2_1_OR_GREATER || NETSTANDARD2_1_OR_GREATER
            System.ReadOnlySpan<byte> data,
#else
            byte[] data,
#endif
            ref int offset)
        {
            if (data[offset++] == Absent)
                return null;

            int count = BinaryWitness.ReadU16(data, ref offset);
            var authorities = new List<string>(count);
            for (int i = 0; i < count; i++)
            {
                authorities.Add(data[offset++] == Absent
                    ? null
                    : BinaryWitness.ReadString(data, ref offset));
            }
            return authorities;
        }
    }
}
