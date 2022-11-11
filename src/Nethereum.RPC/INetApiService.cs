using Nethereum.RPC.Net;

namespace Nethereum.RPC
{
    public interface INetApiService
    {
        INetListening Listening { get; }
        INetPeerCount PeerCount { get; }
        INetVersion Version { get; }
    }
}