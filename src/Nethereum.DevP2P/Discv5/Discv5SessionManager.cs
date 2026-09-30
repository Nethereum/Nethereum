using System;
using System.Collections.Generic;
using System.Net;
using System.Security.Cryptography;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    public class Discv5SessionManager
    {
        public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(1);

        public static readonly TimeSpan HandshakeGcInterval = TimeSpan.FromMilliseconds(500);

        public const int MaxSessions = 1024;

        public const int MaxPendingChallenges = 1024;

        public const int MaxPendingOutbound = 1024;

        private readonly EthECKey _localKey;
        private readonly byte[] _localNodeId;
        private readonly Func<DateTime> _nowUtc;
        private readonly Discv5SessionStore _sessionStore = new(MaxSessions);
        private readonly Discv5PendingChallengeStore _pendingChallengeStore = new(MaxPendingChallenges);
        private readonly Discv5PendingOutboundStore _pendingOutboundStore = new(MaxPendingOutbound);
        private DateTime _lastHandshakeGcUtc;
        private readonly object _gcLock = new object();

        public Discv5SessionManager(EthECKey localKey)
            : this(localKey, null)
        {
        }

        public Discv5SessionManager(EthECKey localKey, Func<DateTime> nowUtc)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _localNodeId = Discv5Crypto.ComputeNodeId(localKey.GetPubKeyNoPrefix());
            _nowUtc = nowUtc ?? (() => DateTime.UtcNow);
            _lastHandshakeGcUtc = _nowUtc();
        }

        public byte[] LocalNodeId => _localNodeId;

        public EthECKey LocalKey => _localKey;

        public class SessionEstablishedEventArgs : EventArgs
        {
            public Discv5Session Session { get; set; }

            public byte[] EnrEncoded { get; set; }
        }

        public event EventHandler<SessionEstablishedEventArgs> SessionEstablished;

        public event EventHandler<SessionEstablishedEventArgs> OutboundSessionEstablished;

        public enum IncomingPacketKind
        {
            Ignored,

            NeedWhoAreYou,

            Decoded
        }

        public class IncomingPacket
        {
            public IncomingPacketKind Kind { get; set; }

            public byte[] OutgoingBytes { get; set; }

            public byte[] Message { get; set; }

            public Discv5Session Session { get; set; }

            public IPEndPoint Source { get; set; }
        }

        private static IncomingPacket Ignored(IPEndPoint source) =>
            new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = source };

        public IncomingPacket Process(byte[] packet, IPEndPoint fromAddr)
        {
            MaybeSweepStaleChallenges();
            try
            {
                var (maskingIv, header, encMsg, rawHeaderForAad) = Discv5Packet.DecodePacket(packet, _localNodeId);

                if (header.Flag != Discv5Packet.PacketFlag.WhoAreYou
                    && (encMsg == null || encMsg.Length < 17))
                {
                    return Ignored(fromAddr);
                }

                switch (header.Flag)
                {
                    case Discv5Packet.PacketFlag.Ordinary:
                        return ProcessOrdinary(maskingIv, header, encMsg, rawHeaderForAad, fromAddr);
                    case Discv5Packet.PacketFlag.Handshake:
                        return ProcessHandshake(maskingIv, header, encMsg, rawHeaderForAad, fromAddr);
                    case Discv5Packet.PacketFlag.WhoAreYou:
                        return ProcessWhoAreYou(maskingIv, header, rawHeaderForAad, fromAddr);
                    default:
                        return Ignored(fromAddr);
                }
            }
            catch (ArgumentException) { return Ignored(fromAddr); }
            catch (InvalidOperationException) { return Ignored(fromAddr); }
            catch (CryptographicException) { return Ignored(fromAddr); }
        }

        public Discv5Session FindSession(byte[] remoteNodeId, IPEndPoint remoteAddr)
        {
            _sessionStore.TryGet(SessionKey(remoteNodeId, remoteAddr), out var session);
            return session;
        }

        private static (byte[] nonce, byte[] maskingIv) NewNonceAndIv()
        {
            var nonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(nonce);
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);
            return (nonce, maskingIv);
        }

        private static byte[] GenerateThrowawayKey()
        {
            var throwawayKey = new byte[16];
            RandomNumberGenerator.Fill(throwawayKey);
            return throwawayKey;
        }

        private static byte[] EncodeOutgoing(
            byte[] maskingIv,
            Discv5Packet.PacketFlag flag,
            byte[] nonce,
            byte[] authData,
            byte[] destNodeId,
            byte[] key,
            byte[] plaintext)
        {
            var header = new Discv5Packet.Header
            {
                Flag = flag,
                Nonce = nonce,
                AuthData = authData
            };
            var rawHeader = Discv5Packet.BuildRawHeader(header);
            var aad = Discv5Packet.BuildAad(maskingIv, rawHeader);
            var encrypted = Discv5Packet.EncryptMessage(key, nonce, aad, plaintext);
            return Discv5Packet.EncodePacket(maskingIv, header, destNodeId, encrypted);
        }

        public byte[] BuildOrdinaryPacket(Discv5Session session, byte[] messagePlaintext)
        {
            var (nonce, maskingIv) = NewNonceAndIv();
            var sendKey = session.IsInitiator ? session.InitiatorKey : session.RecipientKey;
            return EncodeOutgoing(maskingIv, Discv5Packet.PacketFlag.Ordinary, nonce,
                _localNodeId, session.RemoteNodeId, sendKey, messagePlaintext);
        }

        public byte[] BuildInitialOrdinaryPacket(
            byte[] remoteNodeId,
            IPEndPoint remoteAddr,
            byte[] firstMessagePlaintext,
            byte[] peerStaticCompressedPubKey,
            byte[] localEnrEncoded = null)
        {
            if (remoteNodeId == null || remoteNodeId.Length != 32)
                throw new ArgumentException("remote node id must be 32 bytes", nameof(remoteNodeId));
            if (firstMessagePlaintext == null || firstMessagePlaintext.Length == 0)
                throw new ArgumentException("first message must be non-empty", nameof(firstMessagePlaintext));
            if (peerStaticCompressedPubKey == null || peerStaticCompressedPubKey.Length != 33)
                throw new ArgumentException("peer static pubkey must be 33 bytes (compressed)", nameof(peerStaticCompressedPubKey));

            var (nonce, maskingIv) = NewNonceAndIv();

            var sessionKey = SessionKey(remoteNodeId, remoteAddr);
            var pending = new Discv5PendingOutbound
            {
                MessagePlaintext = firstMessagePlaintext,
                PeerStaticPubKey = peerStaticCompressedPubKey,
                LocalEnrEncoded = localEnrEncoded ?? Array.Empty<byte>(),
                InitialNonce = nonce,
                SessionKey = sessionKey,
                RemoteNodeId = remoteNodeId,
                RemoteAddr = remoteAddr,
                CreatedUtc = _nowUtc()
            };
            _pendingOutboundStore.Add(pending);

            var throwawayKey = GenerateThrowawayKey();
            return EncodeOutgoing(maskingIv, Discv5Packet.PacketFlag.Ordinary, nonce,
                _localNodeId, remoteNodeId, throwawayKey, firstMessagePlaintext);
        }

        private (byte[] initiatorKey, byte[] recipientKey) DeriveResponderSessionKeys(
            byte[] sharedSecret, byte[] challengeData, byte[] remoteNodeId) =>
            Discv5KeyDerivation.DeriveSessionKeys(sharedSecret, challengeData, remoteNodeId, _localNodeId);

        private (byte[] initiatorKey, byte[] recipientKey) DeriveInitiatorSessionKeys(
            byte[] sharedSecret, byte[] challengeData, byte[] remoteNodeId) =>
            Discv5KeyDerivation.DeriveSessionKeys(sharedSecret, challengeData, _localNodeId, remoteNodeId);

        private IncomingPacket ProcessOrdinary(
            byte[] maskingIv,
            Discv5Packet.Header header,
            byte[] encMsg,
            byte[] rawHeaderForAad,
            IPEndPoint fromAddr)
        {
            if (header.AuthData == null || header.AuthData.Length != 32)
                return Ignored(fromAddr);
            var srcId = header.AuthData;
            var sessionKey = SessionKey(srcId, fromAddr);

            if (_sessionStore.TryGet(sessionKey, out var session))
            {
                try
                {
                    var aad = Discv5Packet.BuildAad(maskingIv, rawHeaderForAad);
                    var recvKey = session.IsInitiator ? session.RecipientKey : session.InitiatorKey;
                    var plaintext = Discv5Packet.DecryptMessage(recvKey, header.Nonce, aad, encMsg);
                    return new IncomingPacket
                    {
                        Kind = IncomingPacketKind.Decoded,
                        Message = plaintext,
                        Session = session,
                        Source = fromAddr
                    };
                }
                catch (CryptographicException)
                {
                }
            }

            var who = BuildWhoAreYou(srcId, fromAddr, header.Nonce);
            return new IncomingPacket { Kind = IncomingPacketKind.NeedWhoAreYou, OutgoingBytes = who, Source = fromAddr };
        }

        private IncomingPacket ProcessHandshake(
            byte[] maskingIv,
            Discv5Packet.Header header,
            byte[] encMsg,
            byte[] rawHeaderForAad,
            IPEndPoint fromAddr)
        {
            Discv5HandshakePackets.HandshakeAuth auth;
            try { auth = Discv5HandshakePackets.HandshakeAuth.Decode(header.AuthData); }
            catch (Exception) { return Ignored(fromAddr); }

            var srcId = auth.SrcId;
            var pendingKey = SessionKey(srcId, fromAddr);
            var pending = _pendingChallengeStore.GetOrNull(pendingKey);
            if (pending == null)
                return Ignored(fromAddr);

            if (!TryResolveVerifiedStaticPubKey(auth, srcId, out var staticPubKey))
                return Ignored(fromAddr);

            var idSigInputHash = Discv5KeyDerivation.ComputeIdSignatureInput(
                pending.ChallengeData,
                auth.EphemeralPubKey,
                _localNodeId);
            if (!Discv5Crypto.VerifyIdSignature(auth.IdSignature, idSigInputHash, staticPubKey))
                return Ignored(fromAddr);

            byte[] sharedSecret;
            try { sharedSecret = Discv5Crypto.EcdhCompressed(_localKey, auth.EphemeralPubKey); }
            catch (Exception) { return Ignored(fromAddr); }
            var (initKey, recpKey) = DeriveResponderSessionKeys(
                sharedSecret, pending.ChallengeData, srcId);

            var session = new Discv5Session
            {
                RemoteNodeId = srcId,
                RemoteAddr = fromAddr,
                InitiatorKey = initKey,
                RecipientKey = recpKey,
                IsInitiator = false,
                CreatedUtc = _nowUtc()
            };

            byte[] plaintext;
            try
            {
                var aad = Discv5Packet.BuildAad(maskingIv, rawHeaderForAad);
                plaintext = Discv5Packet.DecryptMessage(initKey, header.Nonce, aad, encMsg);
            }
            catch (CryptographicException)
            {
                return Ignored(fromAddr);
            }

            if (!_pendingChallengeStore.Consume(pendingKey, out _))
                return Ignored(fromAddr);
            _sessionStore.Store(SessionKey(srcId, fromAddr), session);
            SessionEstablished?.Invoke(this, new SessionEstablishedEventArgs
            {
                Session = session,
                EnrEncoded = auth.Record
            });
            return new IncomingPacket
            {
                Kind = IncomingPacketKind.Decoded,
                Message = plaintext,
                Session = session,
                Source = fromAddr
            };
        }

        private bool TryResolveVerifiedStaticPubKey(Discv5HandshakePackets.HandshakeAuth auth, byte[] srcId, out byte[] staticPubKey)
        {
            staticPubKey = null;
            if (auth.Record != null && auth.Record.Length > 0)
            {
                try
                {
                    var enr = EnrRecordEncoder.Decode(auth.Record);
                    if (!EnrRecordSigner.Verify(enr))
                        return false;
                    var enrNodeId = Discv5Crypto.ComputeNodeId(enr.Secp256k1);
                    if (!ByteUtil.AreEqual(enrNodeId, srcId))
                        return false;
                    staticPubKey = enr.Secp256k1;
                }
                catch (Exception) { }
            }
            return staticPubKey != null;
        }

        private IncomingPacket ProcessWhoAreYou(
            byte[] maskingIv,
            Discv5Packet.Header header,
            byte[] rawHeaderForAad,
            IPEndPoint fromAddr)
        {
            if (!TryCorrelatePendingOutbound(header, fromAddr, out var pending))
                return Ignored(fromAddr);

            var pendingKey = pending.SessionKey;
            var remoteNodeId = pending.RemoteNodeId;

            Discv5HandshakePackets.WhoAreYouAuth who;
            try { who = Discv5HandshakePackets.WhoAreYouAuth.Decode(header.AuthData); }
            catch (Exception) { return Ignored(fromAddr); }

            if (!TryDeriveInitiatorHandshakeKeys(
                    maskingIv, rawHeaderForAad, remoteNodeId, pendingKey, pending.PeerStaticPubKey,
                    out var ephemPubCompressed, out var idSig, out var initKey, out var recpKey))
                return Ignored(fromAddr);

            var (record, outPacket) = BuildHandshakeReply(who, pending, remoteNodeId, ephemPubCompressed, idSig, initKey);

            CommitOutboundSession(remoteNodeId, fromAddr, initKey, recpKey, pendingKey, record);

            return new IncomingPacket
            {
                Kind = IncomingPacketKind.NeedWhoAreYou,
                OutgoingBytes = outPacket,
                Source = fromAddr
            };
        }

        private bool TryCorrelatePendingOutbound(Discv5Packet.Header header, IPEndPoint fromAddr, out Discv5PendingOutbound pending)
        {
            pending = null;
            if (header.Nonce == null || header.Nonce.Length != Discv5Packet.NonceLength)
                return false;

            if (!_pendingOutboundStore.TryGetByNonce(header.Nonce.ToHex(), out pending) || pending == null)
                return false;
            if (pending.InitialNonce == null
                || !Nethereum.Util.ByteUtil.ConstantTimeEquals(pending.InitialNonce, header.Nonce))
                return false;
            if (pending.RemoteAddr != null && !pending.RemoteAddr.Equals(fromAddr))
                return false;

            return true;
        }

        private bool TryDeriveInitiatorHandshakeKeys(
            byte[] maskingIv,
            byte[] rawHeaderForAad,
            byte[] remoteNodeId,
            string pendingKey,
            byte[] peerStaticPubKey,
            out byte[] ephemPubCompressed,
            out byte[] idSig,
            out byte[] initKey,
            out byte[] recpKey)
        {
            idSig = null;
            initKey = null;
            recpKey = null;

            var ephem = EthECKey.GenerateKey();
            ephemPubCompressed = ephem.GetPubKey(compresseed: true);

            var challengeData = Discv5Packet.BuildAad(maskingIv, rawHeaderForAad);

            var idSigInput = Discv5KeyDerivation.ComputeIdSignatureInput(
                challengeData, ephemPubCompressed, remoteNodeId);
            try { idSig = Discv5Crypto.SignIdSignature(_localKey, idSigInput); }
            catch (Exception)
            {
                _pendingOutboundStore.Remove(pendingKey);
                return false;
            }

            byte[] sharedSecret;
            try { sharedSecret = Discv5Crypto.EcdhCompressed(ephem, peerStaticPubKey); }
            catch (Exception)
            {
                _pendingOutboundStore.Remove(pendingKey);
                return false;
            }

            (initKey, recpKey) = DeriveInitiatorSessionKeys(sharedSecret, challengeData, remoteNodeId);
            return true;
        }

        private (byte[] record, byte[] outPacket) BuildHandshakeReply(
            Discv5HandshakePackets.WhoAreYouAuth who,
            Discv5PendingOutbound pending,
            byte[] remoteNodeId,
            byte[] ephemPubCompressed,
            byte[] idSig,
            byte[] initKey)
        {
            var record = (who.EnrSeq == 0 && pending.LocalEnrEncoded != null && pending.LocalEnrEncoded.Length > 0)
                ? pending.LocalEnrEncoded
                : Array.Empty<byte>();

            var handshakeAuth = new Discv5HandshakePackets.HandshakeAuth
            {
                SrcId = _localNodeId,
                IdSignature = idSig,
                EphemeralPubKey = ephemPubCompressed,
                Record = record
            };

            var (outNonce, outMaskingIv) = NewNonceAndIv();
            var outPacket = EncodeOutgoing(outMaskingIv, Discv5Packet.PacketFlag.Handshake, outNonce,
                handshakeAuth.Encode(), remoteNodeId, initKey, pending.MessagePlaintext);

            return (record, outPacket);
        }

        private void CommitOutboundSession(
            byte[] remoteNodeId,
            IPEndPoint fromAddr,
            byte[] initKey,
            byte[] recpKey,
            string pendingKey,
            byte[] record)
        {
            var session = new Discv5Session
            {
                RemoteNodeId = remoteNodeId,
                RemoteAddr = fromAddr,
                InitiatorKey = initKey,
                RecipientKey = recpKey,
                IsInitiator = true,
                CreatedUtc = _nowUtc()
            };
            _sessionStore.Store(SessionKey(remoteNodeId, fromAddr), session);
            _pendingOutboundStore.Remove(pendingKey);

            OutboundSessionEstablished?.Invoke(this, new SessionEstablishedEventArgs
            {
                Session = session,
                EnrEncoded = record
            });
        }

        private bool IsRetransmissionOfSamePacketWithinHandshakeWindow(Discv5PendingChallenge existing, byte[] originalNonce) =>
            existing != null
            && existing.EncodedPacket != null
            && existing.OriginalNonce != null
            && originalNonce != null
            && Nethereum.Util.ByteUtil.AreEqual(existing.OriginalNonce, originalNonce)
            && _nowUtc() - existing.CreatedUtc <= HandshakeTimeout;

        private byte[] BuildWhoAreYou(byte[] srcId, IPEndPoint fromAddr, byte[] originalNonce)
        {
            var key = SessionKey(srcId, fromAddr);

            var existing = _pendingChallengeStore.GetOrNull(key);
            if (IsRetransmissionOfSamePacketWithinHandshakeWindow(existing, originalNonce))
            {
                return existing.EncodedPacket;
            }

            var idNonce = new byte[Discv5HandshakePackets.WhoAreYouAuth.IdNonceLength];
            RandomNumberGenerator.Fill(idNonce);

            ulong enrSeq = 0;
            var authdata = new Discv5HandshakePackets.WhoAreYouAuth { IdNonce = idNonce, EnrSeq = enrSeq }.Encode();

            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.WhoAreYou,
                Nonce = originalNonce ?? new byte[Discv5Packet.NonceLength],
                AuthData = authdata
            };
            var rawHeader = Discv5Packet.BuildRawHeader(header);

            var challengeData = Discv5Packet.BuildAad(maskingIv, rawHeader);
            var encoded = Discv5Packet.EncodePacket(maskingIv, header, srcId, Array.Empty<byte>());

            _pendingChallengeStore.Add(key, new Discv5PendingChallenge
            {
                IdNonce = idNonce,
                ChallengeData = challengeData,
                EnrSeq = enrSeq,
                OriginalNonce = originalNonce,
                EncodedPacket = encoded,
                CreatedUtc = _nowUtc()
            });
            return encoded;
        }

        private static string SessionKey(byte[] nodeId, IPEndPoint addr)
            => $"{nodeId.ToHex()}|{addr}";

        public int PendingChallengeCount => _pendingChallengeStore.Count;

        public int PendingOutboundCount => _pendingOutboundStore.Count;

        public int PendingOutboundNonceIndexCount => _pendingOutboundStore.NonceIndexCount;

        public readonly struct PendingOutboundInfo
        {
            public PendingOutboundInfo(byte[] remoteNodeId, IPEndPoint remoteAddr, DateTime createdUtc)
            {
                RemoteNodeId = remoteNodeId;
                RemoteAddr = remoteAddr;
                CreatedUtc = createdUtc;
            }

            public byte[] RemoteNodeId { get; }
            public IPEndPoint RemoteAddr { get; }
            public DateTime CreatedUtc { get; }
        }

        public IReadOnlyList<PendingOutboundInfo> SnapshotPendingOutbound()
        {
            var dials = _pendingOutboundStore.Snapshot();
            var snapshot = new List<PendingOutboundInfo>(dials.Count);
            foreach (var dial in dials)
            {
                var remoteNodeId = (byte[])dial.RemoteNodeId.Clone();
                var remoteAddr = new IPEndPoint(dial.RemoteAddr.Address, dial.RemoteAddr.Port);
                snapshot.Add(new PendingOutboundInfo(remoteNodeId, remoteAddr, dial.CreatedUtc));
            }
            return snapshot;
        }

        public int SessionCount => _sessionStore.Count;

        private void MaybeSweepStaleChallenges()
        {
            var now = _nowUtc();
            if (now - _lastHandshakeGcUtc < HandshakeGcInterval) return;
            lock (_gcLock)
            {
                if (now - _lastHandshakeGcUtc < HandshakeGcInterval) return;
                _lastHandshakeGcUtc = now;
            }
            SweepStaleChallenges(now);
        }

        public void SweepStaleChallenges()
            => SweepStaleChallenges(_nowUtc());

        public void SweepStaleChallenges(DateTime now)
        {
            _pendingChallengeStore.SweepOlderThan(now, HandshakeTimeout);
            _pendingOutboundStore.SweepOlderThan(now, HandshakeTimeout);
        }
    }
}
