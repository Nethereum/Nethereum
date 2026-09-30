using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Merkle.Patricia.Storage
{
    public interface ITrieTracer
    {
        void OnRemove(byte[] owner, byte[] path, byte[] prevBlob);

        IReadOnlyCollection<TrieNodeDelete> Removals { get; }

        void Reset();
    }

    public sealed class TrieTracer : ITrieTracer
    {
        private readonly Dictionary<byte[], TrieNodeDelete> _removals =
            new Dictionary<byte[], TrieNodeDelete>(new ByteArrayComparer());

        public void OnRemove(byte[] owner, byte[] path, byte[] prevBlob)
        {
            _removals[Key(owner, path)] = new TrieNodeDelete(owner, path, prevBlob);
        }

        public IReadOnlyCollection<TrieNodeDelete> Removals => new List<TrieNodeDelete>(_removals.Values);

        public void Reset() => _removals.Clear();

        public static byte[] Key(byte[] owner, byte[] path)
            => ByteUtil.Merge(owner ?? new byte[0], path ?? new byte[0]);
    }
}
