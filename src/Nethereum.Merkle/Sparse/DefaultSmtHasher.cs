using System;
using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Sparse
{
    public class DefaultSmtHasher : ISmtHasher
    {
        private readonly IHashProvider _hashProvider;
        private readonly byte[] _emptyLeaf;

        public DefaultSmtHasher(IHashProvider hashProvider)
        {
            _hashProvider = hashProvider ?? throw new ArgumentNullException(nameof(hashProvider));
            _emptyLeaf = _hashProvider.ComputeHash(System.Text.Encoding.UTF8.GetBytes(""));
        }

        public bool MsbFirst => false;

        public bool UseFixedEmptyHash => false;

        public bool CollapseSingleLeaf => false;

        public byte[] EmptyLeaf => _emptyLeaf;

        public byte[] HashLeaf(byte[] path, byte[] valueBytes)
        {
            return _hashProvider.ComputeHash(valueBytes);
        }

        public byte[] HashNode(byte[] leftHash, byte[] rightHash)
        {
            var combined = new byte[leftHash.Length + rightHash.Length];
            Array.Copy(leftHash, 0, combined, 0, leftHash.Length);
            Array.Copy(rightHash, 0, combined, leftHash.Length, rightHash.Length);
            return _hashProvider.ComputeHash(combined);
        }
    }
}
