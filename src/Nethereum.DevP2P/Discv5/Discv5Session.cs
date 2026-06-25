using System;
using System.Net;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// Established discv5 session with a single remote peer. Holds the two
    /// AES-GCM keys produced by the HKDF derivation plus the peer's identity.
    /// <para>
    /// The two keys are direction-agnostic at the protocol level — both sides
    /// agree on the same (InitiatorKey, RecipientKey) pair after handshake.
    /// Per discv5-theory.md §"Session Keys": the initiator encrypts outbound
    /// with <see cref="InitiatorKey"/> and decrypts inbound with <see cref="RecipientKey"/>;
    /// the recipient does the inverse. <see cref="IsInitiator"/> records this
    /// node's role so <see cref="Discv5SessionManager.BuildOrdinaryPacket"/> can
    /// pick the right encryption key.
    /// </para>
    /// </summary>
    public class Discv5Session
    {
        /// <summary>32-byte node id of the peer (<c>keccak256(pubkey-x ‖ pubkey-y)</c>).</summary>
        public byte[] RemoteNodeId { get; set; }

        /// <summary>Remote UDP endpoint we'll send outgoing packets to.</summary>
        public IPEndPoint RemoteAddr { get; set; }

        /// <summary>16-byte AES-GCM key derived from the first half of HKDF output. Initiator encrypts outbound with this; recipient decrypts inbound with this.</summary>
        public byte[] InitiatorKey { get; set; }

        /// <summary>16-byte AES-GCM key derived from the second half of HKDF output. Recipient encrypts outbound with this; initiator decrypts inbound with this.</summary>
        public byte[] RecipientKey { get; set; }

        /// <summary>True if this local node initiated the handshake, false if it responded to one.</summary>
        public bool IsInitiator { get; set; }

        /// <summary>Wall-clock instant the handshake completed and this session was stored.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Outstanding WHOAREYOU challenge sent to a peer. Held until the peer
    /// either replies with a handshake packet (consuming it) or times out.
    /// <see cref="ChallengeData"/> = <c>masking-iv ‖ static-header ‖ authdata</c>
    /// of the WHOAREYOU packet — both sides must reconstruct exactly the same
    /// bytes to derive matching session keys via HKDF.
    /// </summary>
    public class Discv5PendingChallenge
    {
        /// <summary>16-byte challenge nonce the peer must echo in their handshake reply.</summary>
        public byte[] IdNonce { get; set; }

        /// <summary>The 63-byte challenge data we used as HKDF salt and the peer will reconstruct from received bytes.</summary>
        public byte[] ChallengeData { get; set; }

        /// <summary>Responder's view of the peer's latest ENR sequence number, or 0 if unknown.</summary>
        public ulong EnrSeq { get; set; }

        /// <summary>Nonce from the ordinary packet that triggered this WHOAREYOU — echoed in the WHOAREYOU header per spec.</summary>
        public byte[] OriginalNonce { get; set; }

        /// <summary>Wall-clock instant this challenge was issued.</summary>
        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>Fully-encoded WHOAREYOU packet bytes ready to resend on a concurrent re-Unknown from the same peer within the handshake timeout.</summary>
        public byte[] EncodedPacket { get; set; }
    }
}
