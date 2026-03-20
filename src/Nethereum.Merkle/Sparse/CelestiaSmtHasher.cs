using System;
using System.Security.Cryptography;

namespace Nethereum.Merkle.Sparse
{
    public class CelestiaSmtHasher : ISmtHasher
    {
        private const byte LeafPrefix = 0x00;
        private const byte NodePrefix = 0x01;

        [ThreadStatic] private static SHA256 _sha256;

        private static SHA256 Sha256 => _sha256 ??= SHA256.Create();

        public bool MsbFirst => true;

        public bool UseFixedEmptyHash => true;

        public bool CollapseSingleLeaf => true;

        public byte[] EmptyLeaf => new byte[32];

        public byte[] HashLeaf(byte[] path, byte[] valueBytes)
        {
            var sha = Sha256;
            var dataHash = sha.ComputeHash(valueBytes);

            var preimage = new byte[1 + path.Length + dataHash.Length];
            preimage[0] = LeafPrefix;
            Array.Copy(path, 0, preimage, 1, path.Length);
            Array.Copy(dataHash, 0, preimage, 1 + path.Length, dataHash.Length);

            return sha.ComputeHash(preimage);
        }

        public byte[] HashNode(byte[] leftHash, byte[] rightHash)
        {
            var preimage = new byte[1 + leftHash.Length + rightHash.Length];
            preimage[0] = NodePrefix;
            Array.Copy(leftHash, 0, preimage, 1, leftHash.Length);
            Array.Copy(rightHash, 0, preimage, 1 + leftHash.Length, rightHash.Length);

            return Sha256.ComputeHash(preimage);
        }
    }
}
