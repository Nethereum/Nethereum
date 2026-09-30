using System;

namespace Nethereum.DevP2P.Sync.Mempool
{
    public static class TransactionPropagationPolicy
    {
        public static int DirectPeerCount(int peerCount)
        {
            if (peerCount <= 0) return 0;
            return (int)Math.Sqrt(peerCount);
        }
    }
}
