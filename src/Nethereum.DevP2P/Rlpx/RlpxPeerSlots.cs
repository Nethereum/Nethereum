using System.Threading;

namespace Nethereum.DevP2P.Rlpx
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p", "RlpxPeerSlots — claim one slot per admitted peer")]
    public sealed class RlpxPeerSlots
    {
        private readonly int _maxPeers;
        private int _active;

        public RlpxPeerSlots(int maxPeers)
        {
            _maxPeers = maxPeers;
        }

        public int Active => Volatile.Read(ref _active);

        public bool TryAcquire(bool trusted)
        {
            while (true)
            {
                var current = Volatile.Read(ref _active);
                if (!trusted && _maxPeers > 0 && current >= _maxPeers)
                    return false;
                if (Interlocked.CompareExchange(ref _active, current + 1, current) == current)
                    return true;
            }
        }

        public void Release()
        {
            while (true)
            {
                var current = Volatile.Read(ref _active);
                if (current <= 0)
                    return;
                if (Interlocked.CompareExchange(ref _active, current - 1, current) == current)
                    return;
            }
        }
    }
}
