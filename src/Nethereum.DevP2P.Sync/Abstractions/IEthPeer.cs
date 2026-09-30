using System;
using Nethereum.DevP2P.Rlpx;

namespace Nethereum.DevP2P.Sync.Abstractions
{
    public interface IEthPeer
    {
        Guid Id { get; }

        string Enode { get; }

        string Host { get; }

        bool IsTrusted => false;

        int EthVersion { get; }

        ulong PeerLatestBlock { get; }

        uint PeerForkHash { get; }

        RlpxConnection Connection { get; }

        DateTime LastFrameReceivedUtc => DateTime.MinValue;

        event EventHandler<IEthPeer> Disconnected;
    }
}
