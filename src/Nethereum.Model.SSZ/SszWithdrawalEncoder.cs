using System;
using System.Collections.Generic;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Ssz;

namespace Nethereum.Model.SSZ
{
    // EIP-6465: SSZ Withdrawal
    // Withdrawal(ProgressiveContainer(active_fields=[1, 1, 1, 1])):
    //   index: WithdrawalIndex            (uint64, 8 bytes LE)
    //   validator_index: ValidatorIndex   (uint64, 8 bytes LE)
    //   address: ExecutionAddress         (ByteVector[20])
    //   amount: Gwei                      (uint64, 8 bytes LE)
    //
    // All four fields are fixed-size. Serialisation is a straight concatenation
    // (identical layout to a regular Container since there are no variable fields).
    // Merkleisation uses ProgressiveContainer semantics per EIP-7495 — each field's
    // hash_tree_root is mixed into an active_fields bitvector.
    public class SszWithdrawalEncoder
    {
        public const int AddressLength = 20;
        public const int WithdrawalLength = 8 + 8 + AddressLength + 8; // 44 bytes
        public static readonly bool[] ActiveFields = { true, true, true, true };

        public static readonly SszWithdrawalEncoder Current = new SszWithdrawalEncoder();

        public byte[] Encode(ulong index, ulong validatorIndex, byte[] address, ulong amountInGwei)
        {
            if (address == null) address = new byte[AddressLength];
            if (address.Length != AddressLength)
                throw new ArgumentException(
                    $"SSZ Withdrawal: address must be {AddressLength} bytes, got {address.Length}.",
                    nameof(address));

            using var writer = new SszWriter();
            writer.WriteUInt64(index);
            writer.WriteUInt64(validatorIndex);
            writer.WriteFixedBytes(address, AddressLength);
            writer.WriteUInt64(amountInGwei);
            return writer.ToArray();
        }

        public void Decode(ReadOnlySpan<byte> data,
            out ulong index, out ulong validatorIndex, out byte[] address, out ulong amountInGwei)
        {
            if (data.Length != WithdrawalLength)
                throw new ArgumentException(
                    $"SSZ Withdrawal: expected {WithdrawalLength} bytes, got {data.Length}.",
                    nameof(data));

            var reader = new SszReader(data);
            index = reader.ReadUInt64();
            validatorIndex = reader.ReadUInt64();
            address = reader.ReadFixedBytes(AddressLength);
            amountInGwei = reader.ReadUInt64();
        }

        public byte[] HashTreeRoot(ulong index, ulong validatorIndex, byte[] address, ulong amountInGwei)
        {
            var fieldRoots = new List<byte[]>
            {
                SszHashTreeRootHelper.HashTreeRootUint64(index),
                SszHashTreeRootHelper.HashTreeRootUint64(validatorIndex),
                HashTreeRootAddressBytes(address),
                SszHashTreeRootHelper.HashTreeRootUint64(amountInGwei)
            };
            return SszMerkleizer.HashTreeRootProgressiveContainer(fieldRoots, ActiveFields);
        }

        private static byte[] HashTreeRootAddressBytes(byte[] address)
        {
            var chunk = new byte[32];
            if (address != null && address.Length == AddressLength)
                Buffer.BlockCopy(address, 0, chunk, 0, AddressLength);
            return chunk;
        }
    }
}
