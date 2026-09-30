using System;

namespace Nethereum.DevP2P.Discv5
{
    public static class Discv5HandshakePackets
    {
        public class WhoAreYouAuth
        {
            public const int IdNonceLength = 16;

            public const int EnrSeqLength = 8;

            public const int TotalLength = IdNonceLength + EnrSeqLength;

            public byte[] IdNonce { get; set; } = new byte[IdNonceLength];

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

        public class HandshakeAuth
        {
            public const int SrcIdLength = 32;

            public const int FixedPrefixLength = SrcIdLength + 2;

            public const int MaxOneByteFieldLength = 255;

            public byte[] SrcId { get; set; } = new byte[SrcIdLength];

            public byte[] IdSignature { get; set; } = Array.Empty<byte>();

            public byte[] EphemeralPubKey { get; set; } = Array.Empty<byte>();

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
