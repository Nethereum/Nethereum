using System;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// Authdata layouts for the two non-ordinary discv5 packet flags per
    /// discv5-wire.md. Ordinary packets just carry the 32-byte src-id in
    /// authdata; WHOAREYOU and Handshake have their own structures.
    /// </summary>
    public static class Discv5HandshakePackets
    {
        /// <summary>
        /// WHOAREYOU authdata = <c>id-nonce(16) ‖ enr-seq(8)</c>. Sent by the
        /// responder when it can't decrypt an incoming ordinary packet — the
        /// initiator must reply with a Handshake packet proving identity.
        /// </summary>
        public class WhoAreYouAuth
        {
            /// <summary>Length of the id-nonce field per <c>discv5-wire.md §"WHOAREYOU"</c>.</summary>
            public const int IdNonceLength = 16;

            /// <summary>Length of the enr-seq field — big-endian uint64.</summary>
            public const int EnrSeqLength = 8;

            /// <summary>Total authdata length: <see cref="IdNonceLength"/> + <see cref="EnrSeqLength"/>.</summary>
            public const int TotalLength = IdNonceLength + EnrSeqLength;

            /// <summary>16-byte challenge nonce the initiator must echo in their handshake reply.</summary>
            public byte[] IdNonce { get; set; } = new byte[IdNonceLength];

            /// <summary>Responder's view of the initiator's latest ENR sequence number, or 0 if unknown.</summary>
            public ulong EnrSeq { get; set; }

            public byte[] Encode()
            {
                if (IdNonce == null || IdNonce.Length != IdNonceLength)
                    throw new ArgumentException($"id-nonce must be {IdNonceLength} bytes");
                var buf = new byte[TotalLength];
                Buffer.BlockCopy(IdNonce, 0, buf, 0, IdNonceLength);
                for (int i = 0; i < EnrSeqLength; i++)
                    buf[IdNonceLength + i] = (byte)((EnrSeq >> ((EnrSeqLength - 1 - i) * 8)) & 0xff);
                return buf;
            }

            public static WhoAreYouAuth Decode(byte[] authdata)
            {
                if (authdata == null || authdata.Length != TotalLength)
                    throw new ArgumentException($"WHOAREYOU authdata must be {TotalLength} bytes, got {authdata?.Length ?? 0}");
                var idNonce = new byte[IdNonceLength];
                Buffer.BlockCopy(authdata, 0, idNonce, 0, IdNonceLength);
                ulong enrSeq = 0;
                for (int i = 0; i < EnrSeqLength; i++)
                    enrSeq = (enrSeq << 8) | authdata[IdNonceLength + i];
                return new WhoAreYouAuth { IdNonce = idNonce, EnrSeq = enrSeq };
            }
        }

        /// <summary>
        /// Handshake authdata =
        /// src-id(32) || sig-size(1) || eph-key-size(1) || id-signature(sig-size)
        /// || eph-pubkey(eph-key-size) || optional rlp(ENR).
        /// Sent by the initiator in response to a WHOAREYOU challenge — proves
        /// its identity and supplies the ephemeral key the recipient needs to
        /// derive the session keys via ECDH+HKDF.
        /// </summary>
        public class HandshakeAuth
        {
            /// <summary>Discv5 node id (32 bytes) of the initiator.</summary>
            public const int SrcIdLength = 32;

            /// <summary>Bytes preceding the variable-size signature/eph-pubkey/record: <c>src-id + sig-size + key-size</c>.</summary>
            public const int FixedPrefixLength = SrcIdLength + 2;

            /// <summary>Maximum value of the one-byte sig-size and eph-key-size fields.</summary>
            public const int MaxOneByteFieldLength = 255;

            /// <summary>32-byte node id (<c>keccak256(pubkey-x ‖ pubkey-y)</c>) of the initiator.</summary>
            public byte[] SrcId { get; set; } = new byte[SrcIdLength];

            /// <summary>secp256k1 signature (64-byte r ‖ s, no recovery byte) over the id-signature input.</summary>
            public byte[] IdSignature { get; set; } = Array.Empty<byte>();

            /// <summary>33-byte compressed secp256k1 ephemeral public key used for ECDH.</summary>
            public byte[] EphemeralPubKey { get; set; } = Array.Empty<byte>();

            /// <summary>RLP-encoded ENR record of the initiator, or empty if they omitted it.</summary>
            public byte[] Record { get; set; } = Array.Empty<byte>();

            public byte[] Encode()
            {
                if (SrcId == null || SrcId.Length != SrcIdLength)
                    throw new ArgumentException($"src-id must be {SrcIdLength} bytes");
                if (IdSignature.Length > MaxOneByteFieldLength || EphemeralPubKey.Length > MaxOneByteFieldLength)
                    throw new ArgumentException($"sig / ephemeral-pubkey must fit in one byte (max {MaxOneByteFieldLength})");

                var total = FixedPrefixLength + IdSignature.Length + EphemeralPubKey.Length + Record.Length;
                var buf = new byte[total];
                int o = 0;
                Buffer.BlockCopy(SrcId, 0, buf, o, SrcIdLength); o += SrcIdLength;
                buf[o++] = (byte)IdSignature.Length;
                buf[o++] = (byte)EphemeralPubKey.Length;
                Buffer.BlockCopy(IdSignature, 0, buf, o, IdSignature.Length); o += IdSignature.Length;
                Buffer.BlockCopy(EphemeralPubKey, 0, buf, o, EphemeralPubKey.Length); o += EphemeralPubKey.Length;
                if (Record.Length > 0)
                    Buffer.BlockCopy(Record, 0, buf, o, Record.Length);
                return buf;
            }

            public static HandshakeAuth Decode(byte[] authdata)
            {
                if (authdata == null || authdata.Length < FixedPrefixLength)
                    throw new ArgumentException("handshake authdata too short");
                var srcId = new byte[SrcIdLength];
                Buffer.BlockCopy(authdata, 0, srcId, 0, SrcIdLength);
                int o = SrcIdLength;
                int sigLen = authdata[o++];
                int ephLen = authdata[o++];
                if (authdata.Length < o + sigLen + ephLen)
                    throw new ArgumentException("handshake authdata truncated before signature/eph-pubkey");
                var sig = new byte[sigLen];
                Buffer.BlockCopy(authdata, o, sig, 0, sigLen); o += sigLen;
                var eph = new byte[ephLen];
                Buffer.BlockCopy(authdata, o, eph, 0, ephLen); o += ephLen;
                var record = new byte[authdata.Length - o];
                if (record.Length > 0)
                    Buffer.BlockCopy(authdata, o, record, 0, record.Length);
                return new HandshakeAuth
                {
                    SrcId = srcId,
                    IdSignature = sig,
                    EphemeralPubKey = eph,
                    Record = record
                };
            }
        }
    }
}
