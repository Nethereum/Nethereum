using Nethereum.Besu.RPC.Admin;

namespace Nethereum.Besu
{
    public interface IAdminApiService
    {
        IAdminAddPeer AddPeer { get; }
        IAdminNodeInfo NodeInfo { get; }
        IAdminPeers Peers { get; }
        IAdminRemovePeer RemovePeer { get; }
    }
}