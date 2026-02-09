using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia
{
    public abstract class Node
    {
        protected IHashProvider HashProvider { get; set; }

        private byte[] _cachedHash;
        private byte[] _cachedRlpData;
        private bool _dirty = true;

        public Node(IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
        }

        public abstract byte[] GetRLPEncodedDataCore();

        public byte[] GetRLPEncodedData()
        {
            if (!_dirty && _cachedRlpData != null)
                return _cachedRlpData;

            _cachedRlpData = GetRLPEncodedDataCore();
            return _cachedRlpData;
        }

        public virtual byte[] GetHash()
        {
            if (!_dirty && _cachedHash != null)
                return _cachedHash;

            _cachedHash = HashProvider.ComputeHash(GetRLPEncodedData());
            _dirty = false;
            return _cachedHash;
        }

        public void MarkDirty()
        {
            _dirty = true;
            _cachedHash = null;
            _cachedRlpData = null;
        }

        public bool IsDirty => _dirty;

        protected void InvalidateCache()
        {
            MarkDirty();
        }
    }
}
