using System;
using System.Net;
using Nethereum.DevP2P.Sync.Abstractions;
using Nethereum.Model.P2P;

namespace Nethereum.DevP2P.Sync.Serving
{
    public sealed class PeerListenerOptions
    {
        public int ListenPort { get; set; } = 30303;

        public IPAddress BindAddress { get; set; }

        public int MaxInboundPeers { get; set; } = 50;

        public int MaxInboundPerIP { get; set; } = 3;

        public int MaxFrameSize { get; set; } = 16 * 1024 * 1024;

        public int HandshakeTimeoutMs { get; set; } = 10_000;

        public TimeSpan IdleTimeout { get; set; } = TimeSpan.FromMinutes(2);

        public bool ServeSnap { get; set; } = true;

        public bool AdvertiseSnap2 { get; set; } = false;

        public bool EnableUPnP { get; set; } = false;

        public string[] TrustedNodeIds { get; set; } = Array.Empty<string>();

        public string ClientId { get; set; } = "Nethereum.DevP2P.Sync/0.1";

        public bool MirrorRemoteStatus { get; set; } = true;

        public Action<string> OnInboundPeerAdded { get; set; }

        public Action<string> OnInboundPeerRemoved { get; set; }

        public Nethereum.CoreChain.ITxPool TxPool { get; set; }

        public Action<NewPooledTransactionHashesMessage> OnPooledTransactionHashesReceived { get; set; }

        public Action<TransactionsMessage> OnTransactionsReceived { get; set; }

        public Action<TransactionsMessage> OnTrustedTransactionsReceived { get; set; }

        public Action<TransactionsMessage, Guid> OnTrustedTransactionsReceivedFrom { get; set; }

        public IEthPeerRegistry EthPeerRegistry { get; set; }

        public Action<NewBlockMessage> OnNewBlockReceived { get; set; }
    }
}
