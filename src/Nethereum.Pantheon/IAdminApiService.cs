using Nethereum.Pantheon.RPC.Admin;

namespace Nethereum.Pantheon
{
    public interface IAdminApiService
    {
        IAdminAddPeer AddPeer { get; }
        IAdminNodeInfo NodeInfo { get; }
        IAdminPeers Peers { get; }
        IAdminRemovePeer RemovePeer { get; }
    }
}