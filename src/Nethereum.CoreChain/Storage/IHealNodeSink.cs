using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain.Storage
{
    public enum HealNodePresence
    {
        Match,
        Absent,
        Stale,
    }

    public interface IHealNodeSink
    {
        bool HasNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash);

        HealNodePresence Probe(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
            => HasNode(isStorage, accountHash, nibblePath, expectedHash)
                ? HealNodePresence.Match
                : HealNodePresence.Absent;

        void PutNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash, byte[] blob);

        void WipeStorage(byte[] accountHash);

        void ForceWipeStorage(byte[] accountHash) => WipeStorage(accountHash);

        bool HasRoot(byte[] root);

        void Flush();
    }

    public interface IHealNodeSinkProvider
    {
        IHealNodeSink CreateHealSink();
    }

    public sealed class HashHealNodeSink : IHealNodeSink
    {
        private readonly INodeBlobStore _store;

        public HashHealNodeSink(INodeBlobStore store)
            => _store = store ?? throw new System.ArgumentNullException(nameof(store));

        public bool HasNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash)
            => _store.Get(expectedHash) != null;

        public void PutNode(bool isStorage, byte[] accountHash, byte[] nibblePath, byte[] expectedHash, byte[] blob)
            => _store.Put(expectedHash, blob);

        public void WipeStorage(byte[] accountHash) { }

        public void ForceWipeStorage(byte[] accountHash) { }

        public bool HasRoot(byte[] root) => _store.Get(root) != null;

        public void Flush() { }
    }
}
