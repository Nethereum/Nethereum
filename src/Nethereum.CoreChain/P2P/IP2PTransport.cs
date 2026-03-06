using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Nethereum.CoreChain.P2P
{
    public interface IP2PTransport : IDisposable
    {
        string NodeId { get; }
        bool IsRunning { get; }
        IReadOnlyCollection<string> ConnectedPeers { get; }

        event EventHandler<P2PMessageEventArgs>? MessageReceived;
        event EventHandler<PeerEventArgs>? PeerConnected;
        event EventHandler<PeerEventArgs>? PeerDisconnected;

        Task StartAsync(CancellationToken cancellationToken = default);
        Task StopAsync();

        Task ConnectAsync(string peerId, string endpoint);
        Task DisconnectAsync(string peerId);

        Task BroadcastAsync(P2PMessage message, CancellationToken cancellationToken = default);
        Task SendAsync(string peerId, P2PMessage message, CancellationToken cancellationToken = default);

        bool IsConnected(string peerId);
    }

    public class P2PMessage
    {
        public P2PMessageType Type { get; set; }
        public byte[] Payload { get; set; } = Array.Empty<byte>();
        public string? SourcePeerId { get; set; }
        public long Timestamp { get; set; } = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        public ulong Nonce { get; set; }
        public byte[]? Signature { get; set; }

        public P2PMessage() { }

        public P2PMessage(P2PMessageType type, byte[] payload)
        {
            Type = type;
            Payload = payload;
        }

        public static P2PMessage Ping() => new P2PMessage(P2PMessageType.Ping, Array.Empty<byte>());
        public static P2PMessage Pong() => new P2PMessage(P2PMessageType.Pong, Array.Empty<byte>());
    }

    public enum P2PMessageType
    {
        Ping = 0x00,
        Pong = 0x01,
        Hello = 0x02,
        Disconnect = 0x03,
        AuthChallenge = 0x04,
        AuthResponse = 0x05,
        GetPeers = 0x06,
        Peers = 0x07,
        Status = 0x10,
        NewBlock = 0x11,
        NewBlockHashes = 0x12,
        GetBlocks = 0x13,
        Blocks = 0x14,
        NewTransaction = 0x20,
        NewTransactionHashes = 0x21,
        GetPooledTransactions = 0x22,
        PooledTransactions = 0x23,
        GetBlockHeaders = 0x30,
        BlockHeaders = 0x31,
        GetBlockBodies = 0x32,
        BlockBodies = 0x33,
        GetNodeData = 0x40,
        NodeData = 0x41,
        Handshake = 0xF0,
        ConsensusSpecific = 0xE0
    }

    public class P2PMessageEventArgs : EventArgs
    {
        public string PeerId { get; }
        public P2PMessage Message { get; }

        public P2PMessageEventArgs(string peerId, P2PMessage message)
        {
            PeerId = peerId;
            Message = message;
        }
    }

    public class PeerEventArgs : EventArgs
    {
        public string PeerId { get; }
        public string? Endpoint { get; }

        public PeerEventArgs(string peerId, string? endpoint = null)
        {
            PeerId = peerId;
            Endpoint = endpoint;
        }
    }

    public class TransportConfig
    {
        public string ListenAddress { get; set; } = "0.0.0.0";
        public int ListenPort { get; set; } = 30303;
        public string NodeId { get; set; } = Guid.NewGuid().ToString("N")[..16];
        public int MaxConnections { get; set; } = 25;
        public int ConnectionTimeoutMs { get; set; } = 10000;
        public int HeartbeatIntervalMs { get; set; } = 15000;
        public int MaxMessageSizeBytes { get; set; } = 10 * 1024 * 1024;

        public static TransportConfig Default => new();
    }

    public class StatusMessage
    {
        public int ProtocolVersion { get; set; } = 1;
        public long ChainId { get; set; }
        public long BlockHeight { get; set; }
        public byte[] BestBlockHash { get; set; } = Array.Empty<byte>();
        public byte[] GenesisHash { get; set; } = Array.Empty<byte>();
        public string NodeId { get; set; } = "";
    }

    public class NewBlockMessage
    {
        public byte[] BlockHeader { get; set; } = Array.Empty<byte>();
        public byte[][] Transactions { get; set; } = Array.Empty<byte[]>();
        public long TotalDifficulty { get; set; }
    }

    public class NewBlockHashesMessage
    {
        public BlockHashNumber[] Hashes { get; set; } = Array.Empty<BlockHashNumber>();
    }

    public class BlockHashNumber
    {
        public byte[] Hash { get; set; } = Array.Empty<byte>();
        public long Number { get; set; }
    }

    public class GetBlocksMessage
    {
        public byte[][] Hashes { get; set; } = Array.Empty<byte[]>();
    }

    public class TransactionHashesMessage
    {
        public byte[][] Hashes { get; set; } = Array.Empty<byte[]>();
    }

    public class PooledTransactionsMessage
    {
        public byte[][] Transactions { get; set; } = Array.Empty<byte[]>();
    }

    public class HelloMessage
    {
        public const int CurrentProtocolVersion = 1;

        public int ProtocolVersion { get; set; } = CurrentProtocolVersion;
        public string NodeId { get; set; } = "";
        public long ChainId { get; set; }
        public int ListenPort { get; set; }
        public string[] Capabilities { get; set; } = Array.Empty<string>();
        public string ClientVersion { get; set; } = "Nethereum/1.0";
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write(ProtocolVersion);
            writer.Write(NodeId);
            writer.Write(ChainId);
            writer.Write(ListenPort);
            writer.Write(Capabilities.Length);
            foreach (var cap in Capabilities)
                writer.Write(cap);
            writer.Write(ClientVersion);
            writer.Write(PublicKey.Length);
            writer.Write(PublicKey);
            return ms.ToArray();
        }

        public static HelloMessage Deserialize(byte[] data)
        {
            if (data == null || data.Length < 20)
                throw new System.IO.InvalidDataException("Invalid Hello message data");

            using var ms = new System.IO.MemoryStream(data);
            using var reader = new System.IO.BinaryReader(ms);
            var msg = new HelloMessage
            {
                ProtocolVersion = reader.ReadInt32(),
                NodeId = reader.ReadString(),
                ChainId = reader.ReadInt64(),
                ListenPort = reader.ReadInt32()
            };
            var capCount = reader.ReadInt32();
            if (capCount < 0 || capCount > 100)
                throw new System.IO.InvalidDataException($"Invalid capability count: {capCount}");
            var caps = new string[capCount];
            for (int i = 0; i < capCount; i++)
                caps[i] = reader.ReadString();
            msg.Capabilities = caps;
            msg.ClientVersion = reader.ReadString();
            var pubKeyLen = reader.ReadInt32();
            if (pubKeyLen < 0 || pubKeyLen > 256)
                throw new System.IO.InvalidDataException($"Invalid public key length: {pubKeyLen}");
            msg.PublicKey = reader.ReadBytes(pubKeyLen);
            return msg;
        }
    }

    public class AuthChallengeMessage
    {
        public byte[] Challenge { get; set; } = Array.Empty<byte>();
        public long Timestamp { get; set; }

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write(Challenge.Length);
            writer.Write(Challenge);
            writer.Write(Timestamp);
            return ms.ToArray();
        }

        public static AuthChallengeMessage Deserialize(byte[] data)
        {
            if (data == null || data.Length < 12)
                throw new System.IO.InvalidDataException("Invalid AuthChallenge message data");

            using var ms = new System.IO.MemoryStream(data);
            using var reader = new System.IO.BinaryReader(ms);
            var len = reader.ReadInt32();
            if (len < 0 || len > 256)
                throw new System.IO.InvalidDataException($"Invalid challenge length: {len}");
            return new AuthChallengeMessage
            {
                Challenge = reader.ReadBytes(len),
                Timestamp = reader.ReadInt64()
            };
        }
    }

    public class AuthResponseMessage
    {
        public byte[] Signature { get; set; } = Array.Empty<byte>();
        public string Address { get; set; } = "";

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write(Signature.Length);
            writer.Write(Signature);
            writer.Write(Address);
            return ms.ToArray();
        }

        public static AuthResponseMessage Deserialize(byte[] data)
        {
            if (data == null || data.Length < 8)
                throw new System.IO.InvalidDataException("Invalid AuthResponse message data");

            using var ms = new System.IO.MemoryStream(data);
            using var reader = new System.IO.BinaryReader(ms);
            var len = reader.ReadInt32();
            if (len < 0 || len > 256)
                throw new System.IO.InvalidDataException($"Invalid signature length: {len}");
            return new AuthResponseMessage
            {
                Signature = reader.ReadBytes(len),
                Address = reader.ReadString()
            };
        }
    }

    public class PeerInfo
    {
        public string NodeId { get; set; } = "";
        public string Address { get; set; } = "";
        public int Port { get; set; }
        public long LastSeen { get; set; }
        public int ReputationScore { get; set; }

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write(NodeId);
            writer.Write(Address);
            writer.Write(Port);
            writer.Write(LastSeen);
            writer.Write(ReputationScore);
            return ms.ToArray();
        }

        public static PeerInfo Deserialize(System.IO.BinaryReader reader)
        {
            return new PeerInfo
            {
                NodeId = reader.ReadString(),
                Address = reader.ReadString(),
                Port = reader.ReadInt32(),
                LastSeen = reader.ReadInt64(),
                ReputationScore = reader.ReadInt32()
            };
        }
    }

    public class GetPeersMessage
    {
        public int MaxPeers { get; set; } = 25;

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write(MaxPeers);
            return ms.ToArray();
        }

        public static GetPeersMessage Deserialize(byte[] data)
        {
            using var ms = new System.IO.MemoryStream(data);
            using var reader = new System.IO.BinaryReader(ms);
            return new GetPeersMessage { MaxPeers = reader.ReadInt32() };
        }
    }

    public class PeersMessage
    {
        public PeerInfo[] Peers { get; set; } = Array.Empty<PeerInfo>();

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write(Peers.Length);
            foreach (var peer in Peers)
            {
                var peerData = peer.Serialize();
                writer.Write(peerData.Length);
                writer.Write(peerData);
            }
            return ms.ToArray();
        }

        public static PeersMessage Deserialize(byte[] data)
        {
            if (data == null || data.Length < 4)
                throw new System.IO.InvalidDataException("Invalid Peers message data");

            using var ms = new System.IO.MemoryStream(data);
            using var reader = new System.IO.BinaryReader(ms);
            var count = reader.ReadInt32();
            if (count < 0 || count > 1000)
                throw new System.IO.InvalidDataException($"Invalid peer count: {count}");
            var peers = new PeerInfo[count];
            for (int i = 0; i < count; i++)
            {
                var len = reader.ReadInt32();
                if (len < 0 || len > 10000)
                    throw new System.IO.InvalidDataException($"Invalid peer data length: {len}");
                var peerData = reader.ReadBytes(len);
                using var peerMs = new System.IO.MemoryStream(peerData);
                using var peerReader = new System.IO.BinaryReader(peerMs);
                peers[i] = PeerInfo.Deserialize(peerReader);
            }
            return new PeersMessage { Peers = peers };
        }
    }

    public class DisconnectMessage
    {
        public DisconnectReason Reason { get; set; }
        public string? Details { get; set; }

        public byte[] Serialize()
        {
            using var ms = new System.IO.MemoryStream();
            using var writer = new System.IO.BinaryWriter(ms);
            writer.Write((int)Reason);
            writer.Write(Details ?? "");
            return ms.ToArray();
        }

        public static DisconnectMessage Deserialize(byte[] data)
        {
            using var ms = new System.IO.MemoryStream(data);
            using var reader = new System.IO.BinaryReader(ms);
            return new DisconnectMessage
            {
                Reason = (DisconnectReason)reader.ReadInt32(),
                Details = reader.ReadString()
            };
        }
    }

    public enum DisconnectReason
    {
        Requested = 0,
        TcpError = 1,
        ProtocolBreach = 2,
        UselessPeer = 3,
        TooManyPeers = 4,
        AlreadyConnected = 5,
        IncompatibleProtocol = 6,
        InvalidIdentity = 7,
        ClientQuit = 8,
        UnexpectedIdentity = 9,
        SameIdentity = 10,
        Timeout = 11,
        AuthenticationFailed = 12,
        ChainIdMismatch = 13
    }

    public enum PeerState
    {
        Connecting,
        Handshaking,
        Authenticating,
        Connected,
        Disconnecting,
        Disconnected
    }
}
