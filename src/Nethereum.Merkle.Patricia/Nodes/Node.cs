using Nethereum.Util.HashProviders;

namespace Nethereum.Merkle.Patricia.Nodes
{
    public abstract class Node
    {
        protected IHashProvider HashProvider { get; set; }

        private byte[] _cachedHash;
        private byte[] _cachedEncoded;
        private bool _dirty = true;
        private bool _needsPersist = true;

        public Node(IHashProvider hashProvider)
        {
            HashProvider = hashProvider;
        }

        public byte[] Owner { get; set; }
        public byte[] Path { get; set; }

        protected abstract byte[] EncodeCore();

        public byte[] GetEncodedData()
        {
            if (!_dirty && _cachedEncoded != null)
                return _cachedEncoded;

            _cachedEncoded = EncodeCore();
            return _cachedEncoded;
        }

        public virtual byte[] GetHash()
        {
            if (!_dirty && _cachedHash != null)
                return _cachedHash;

            _cachedHash = HashProvider.ComputeHash(GetEncodedData());
            _dirty = false;
            return _cachedHash;
        }

        public void MarkDirty()
        {
            _dirty = true;
            _needsPersist = true;
            _cachedHash = null;
            _cachedEncoded = null;
        }

        public bool IsDirty => _dirty;

        public bool NeedsPersist => _needsPersist;

        public void ClearNeedsPersist()
        {
            _needsPersist = false;
        }

        public void MarkPersisted()
        {
            _needsPersist = false;
        }

        protected void InvalidateCache()
        {
            MarkDirty();
        }
    }
}
