using System.Collections.Generic;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public interface IBlockAccessListPeerSource
    {
        IReadOnlyList<IBlockAccessListPeer> GetServiceablePeers();
    }
}
