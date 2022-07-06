using Nethereum.Geth.RPC.Admin;

namespace Nethereum.Geth
{
    public interface IAdminApiService
    {
         IAdminAddPeer AddPeer { get; }
         IAdminRemovePeer RemovePeer { get; }
         IAdminAddTrustedPeer AddTrustedPeer { get; }
         IAdminRemoveTrustedPeer RemoveTrustedPeer { get; }
         IAdminStartHTTP StartHttp { get; }
         IAdminStopHTTP StopHttp { get; }
         IAdminExportChain ExportChain { get; }
         IAdminImportChain ImportChain { get; }
         IAdminDatadir Datadir { get; }
         IAdminNodeInfo NodeInfo { get; }
         IAdminStartRPC StartRPC { get; }
         IAdminStartWS StartWS { get; }
         IAdminStopRPC StopRPC { get; }
         IAdminStopWS StopWS { get; }
         IAdminPeers Peers { get; }
    }
}