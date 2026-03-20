using System;
using System.Numerics;
using Nethereum.Util;

namespace Nethereum.Merkle.Sparse
{
    public class PoseidonSmtHasher : ISmtHasher
    {
        private readonly PoseidonHasher _leafHasher;
        private readonly PoseidonHasher _nodeHasher;

        public PoseidonSmtHasher()
            : this(
                new PoseidonHasher(PoseidonParameterPreset.CircomT3),
                new PoseidonHasher(PoseidonParameterPreset.CircomT2))
        {
        }

        public PoseidonSmtHasher(PoseidonHasher leafHasher, PoseidonHasher nodeHasher)
        {
            _leafHasher = leafHasher ?? throw new ArgumentNullException(nameof(leafHasher));
            _nodeHasher = nodeHasher ?? throw new ArgumentNullException(nameof(nodeHasher));
        }

        public bool MsbFirst => false;

        public bool UseFixedEmptyHash => true;

        public bool CollapseSingleLeaf => true;

        public byte[] EmptyLeaf => new byte[32];

        public byte[] HashLeaf(byte[] path, byte[] valueBytes)
        {
            var key = BytesToFieldElement(path);
            var value = BytesToFieldElement(valueBytes);
            var result = _leafHasher.Hash(key, value, BigInteger.One);
            return FieldElementToBytes(result);
        }

        public byte[] HashNode(byte[] leftHash, byte[] rightHash)
        {
            var left = BytesToFieldElement(leftHash);
            var right = BytesToFieldElement(rightHash);
            var result = _nodeHasher.Hash(left, right);
            return FieldElementToBytes(result);
        }

        private static BigInteger BytesToFieldElement(byte[] bytes)
        {
            if (bytes == null || bytes.Length == 0)
                return BigInteger.Zero;

            var unsigned = new byte[bytes.Length + 1];
            for (int i = 0; i < bytes.Length; i++)
                unsigned[i] = bytes[bytes.Length - 1 - i];

            return new BigInteger(unsigned);
        }

        private static byte[] FieldElementToBytes(BigInteger value)
        {
            if (value.IsZero)
                return new byte[32];

            var littleEndian = value.ToByteArray();
            var result = new byte[32];
            int copyLen = Math.Min(littleEndian.Length, 32);

            for (int i = 0; i < copyLen; i++)
            {
                if (i == littleEndian.Length - 1 && littleEndian[i] == 0)
                    break;
                result[31 - i] = littleEndian[i];
            }

            return result;
        }
    }
}
