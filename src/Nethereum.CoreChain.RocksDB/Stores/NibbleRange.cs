using System.Threading;

namespace Nethereum.CoreChain.RocksDB.Stores
{
    internal sealed class NibbleRange
    {
        private readonly byte[] _owner;

        public NibbleRange(byte[] owner, int nibble)
        {
            _owner = owner;
            Nibble = nibble;
        }

        public int Nibble { get; }
        public RangeHash Hashed { get; private set; }

        public long Hash(FlatReconcileEnv env, CancellationToken ct)
        {
            Hashed = RangeHash.OfFlatSlots(env.Rocks, _owner, new[] { (byte)Nibble }, ct);
            return Hashed.Count;
        }
    }
}
