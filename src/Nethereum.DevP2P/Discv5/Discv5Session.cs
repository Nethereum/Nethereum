using System;
using System.Net;

namespace Nethereum.DevP2P.Discv5
{
    public class Discv5Session
    {
        public byte[] RemoteNodeId { get; set; }

        public IPEndPoint RemoteAddr { get; set; }

        public byte[] InitiatorKey { get; set; }

        public byte[] RecipientKey { get; set; }

        public bool IsInitiator { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    public class Discv5PendingChallenge
    {
        public byte[] IdNonce { get; set; }

        public byte[] ChallengeData { get; set; }

        public ulong EnrSeq { get; set; }

        public byte[] OriginalNonce { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        public byte[] EncodedPacket { get; set; }
    }
}
