using System;
using System.Collections.Concurrent;
using System.Net;
using System.Security.Cryptography;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.Enr;
using Nethereum.Signer;
using Nethereum.Signer.Enr;
using Nethereum.Util;

namespace Nethereum.DevP2P.Discv5
{
    /// <summary>
    /// Drives the discv5 session state machine for one local node — the role
    /// here is "responder": peers initiate sessions against us, we issue
    /// WHOAREYOU challenges, validate the handshake reply, derive session keys
    /// via HKDF-SHA256, and thereafter encrypt/decrypt ordinary message packets.
    /// <para>
    /// Sessions are keyed by <c>(remoteNodeId, remoteAddr)</c> so the same peer
    /// dialling from a new IP triggers a fresh challenge — required by the
    /// PingMultiIP sub-test of <c>devp2p discv5 test</c>.
    /// </para>
    /// </summary>
    public class Discv5SessionManager
    {
        /// <summary>
        /// How long an in-flight WHOAREYOU challenge stays valid for tie-breaking
        /// on a concurrent re-Unknown from the same peer. Within this window we
        /// resend the existing challenge instead of generating a fresh one — so a
        /// peer's in-flight handshake reply remains valid. Mirrors the discv5
        /// handshake timeout used by the reference implementation.
        /// </summary>
        public static readonly TimeSpan HandshakeTimeout = TimeSpan.FromSeconds(1);

        /// <summary>
        /// Cap on accumulated WHOAREYOU challenges awaiting their handshake reply.
        /// A peer who never finishes the handshake leaves a challenge entry behind;
        /// without expiry an attacker could exhaust memory by spamming ordinary
        /// packets that trigger fresh challenges. Sweep runs opportunistically on
        /// every <see cref="Process"/> call once per <see cref="HandshakeGcInterval"/>.
        /// </summary>
        public static readonly TimeSpan HandshakeGcInterval = TimeSpan.FromMilliseconds(500);

        /// <summary>
        /// Cap on the number of established sessions held in memory. Mirrors the
        /// reference implementation's session-cache table size — once we reach
        /// the cap the oldest session (by <see cref="Discv5Session.CreatedUtc"/>)
        /// is evicted on the next insert.
        /// </summary>
        public const int MaxSessions = 1024;

        private readonly EthECKey _localKey;
        private readonly byte[] _localNodeId;
        private readonly ConcurrentDictionary<string, Discv5Session> _sessions = new();
        private readonly ConcurrentDictionary<string, Discv5PendingChallenge> _pendingChallenges = new();
        private readonly ConcurrentDictionary<string, PendingOutbound> _pendingOutbound = new();
        private DateTime _lastHandshakeGcUtc = DateTime.UtcNow;
        private readonly object _gcLock = new object();

        /// <summary>
        /// First-contact outbound message held until the peer answers our
        /// unauthenticated ordinary packet with a WHOAREYOU and we complete the
        /// handshake. <see cref="ProcessWhoAreYou"/> re-encrypts
        /// <see cref="MessagePlaintext"/> under the freshly derived session key.
        /// </summary>
        private sealed class PendingOutbound
        {
            public byte[] MessagePlaintext;
            public byte[] PeerStaticPubKey;     // 33-byte compressed
            public byte[] LocalEnrEncoded;      // optional ENR to surface in handshake
            public DateTime CreatedUtc = DateTime.UtcNow;
        }

        public Discv5SessionManager(EthECKey localKey)
        {
            _localKey = localKey ?? throw new ArgumentNullException(nameof(localKey));
            _localNodeId = Discv5Crypto.ComputeNodeId(localKey.GetPubKeyNoPrefix());
        }

        /// <summary>The 32-byte node id of this local node: <c>keccak256(pubkey-x ‖ pubkey-y)</c>.</summary>
        public byte[] LocalNodeId => _localNodeId;

        /// <summary>Static secp256k1 key for this local node.</summary>
        public EthECKey LocalKey => _localKey;

        /// <summary>Event args for <see cref="SessionEstablished"/>.</summary>
        public class SessionEstablishedEventArgs : EventArgs
        {
            /// <summary>The session just stored, including derived AES-GCM keys.</summary>
            public Discv5Session Session { get; set; }

            /// <summary>The peer's ENR (RLP-encoded) carried inside the handshake authdata, or null.</summary>
            public byte[] EnrEncoded { get; set; }
        }

        /// <summary>Fires once when a peer's handshake completes and a session is stored.</summary>
        public event EventHandler<SessionEstablishedEventArgs> SessionEstablished;

        /// <summary>Fires once when our outbound-initiated handshake completes and the session is stored.</summary>
        public event EventHandler<SessionEstablishedEventArgs> OutboundSessionEstablished;

        /// <summary>What the listener should do with a packet after <see cref="Process"/> returns.</summary>
        public enum IncomingPacketKind
        {
            /// <summary>Packet was malformed, replayed, or otherwise unactionable.</summary>
            Ignored,

            /// <summary>Packet couldn't be decrypted against any active session — send <see cref="IncomingPacket.OutgoingBytes"/> as a WHOAREYOU challenge.</summary>
            NeedWhoAreYou,

            /// <summary>Packet was decrypted successfully — dispatch <see cref="IncomingPacket.Message"/> as a discv5 message.</summary>
            Decoded
        }

        /// <summary>Output of <see cref="Process"/> — the next action the listener should take.</summary>
        public class IncomingPacket
        {
            /// <summary>The decoded session-layer outcome.</summary>
            public IncomingPacketKind Kind { get; set; }

            /// <summary>For <see cref="IncomingPacketKind.NeedWhoAreYou"/>: the WHOAREYOU packet bytes ready to send.</summary>
            public byte[] OutgoingBytes { get; set; }

            /// <summary>For <see cref="IncomingPacketKind.Decoded"/>: the plaintext discv5 message ("type ‖ rlp(body)").</summary>
            public byte[] Message { get; set; }

            /// <summary>For <see cref="IncomingPacketKind.Decoded"/>: the session we decoded against.</summary>
            public Discv5Session Session { get; set; }

            /// <summary>The remote endpoint the packet came from.</summary>
            public IPEndPoint Source { get; set; }
        }

        /// <summary>
        /// Process a freshly-arrived UDP packet from <paramref name="fromAddr"/>.
        /// Returns the next action the listener should take.
        /// </summary>
        public IncomingPacket Process(byte[] packet, IPEndPoint fromAddr)
        {
            MaybeSweepStaleChallenges();
            try
            {
                var (maskingIv, header, encMsg, rawHeaderForAad) = Discv5Packet.DecodePacket(packet, _localNodeId);

                // Per discv5-wire.md §"Packet Encoding": a non-WHOAREYOU packet
                // must carry at least a 16-byte GCM tag plus one byte of
                // ciphertext. Reject before invoking AES-GCM, which would
                // otherwise raise a noisier CryptographicException.
                if (header.Flag != Discv5Packet.PacketFlag.WhoAreYou
                    && (encMsg == null || encMsg.Length < 17))
                {
                    return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
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
                        return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
                }
            }
            // Malformed / truncated / MAC-mismatch / wrong-protocol packets all drop here —
            // anything else (OOM, ThreadAbort, etc.) must propagate.
            catch (ArgumentException) { return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr }; }
            catch (InvalidOperationException) { return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr }; }
            catch (CryptographicException) { return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr }; }
        }

        /// <summary>Look up an existing session for a (nodeId, addr) pair, or null.</summary>
        public Discv5Session FindSession(byte[] remoteNodeId, IPEndPoint remoteAddr)
        {
            _sessions.TryGetValue(SessionKey(remoteNodeId, remoteAddr), out var session);
            return session;
        }

        /// <summary>Encode an outgoing ordinary message packet to <paramref name="session"/>.</summary>
        public byte[] BuildOrdinaryPacket(Discv5Session session, byte[] messagePlaintext)
        {
            var nonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(nonce);
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = nonce,
                AuthData = _localNodeId   // ordinary authdata = 32-byte src-id
            };
            var rawHeader = Discv5Packet.BuildRawHeader(header);
            var aad = Discv5Packet.BuildAad(maskingIv, rawHeader);
            var sendKey = session.IsInitiator ? session.InitiatorKey : session.RecipientKey;
            var encrypted = Discv5Packet.EncryptMessage(sendKey, nonce, aad, messagePlaintext);
            return Discv5Packet.EncodePacket(maskingIv, header, session.RemoteNodeId, encrypted);
        }

        /// <summary>
        /// Build the first ordinary packet to a peer we have no session with.
        /// The packet is encrypted under a throwaway random key so the peer
        /// fails to decrypt and replies with WHOAREYOU. The plaintext is
        /// stashed in <c>_pendingOutbound</c> so we can re-emit it under the
        /// real session key once the handshake completes.
        /// </summary>
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

            _pendingOutbound[SessionKey(remoteNodeId, remoteAddr)] = new PendingOutbound
            {
                MessagePlaintext = firstMessagePlaintext,
                PeerStaticPubKey = peerStaticCompressedPubKey,
                LocalEnrEncoded = localEnrEncoded ?? Array.Empty<byte>()
            };

            var nonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(nonce);
            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);

            // Random throwaway key — peer cannot decrypt, will respond WHOAREYOU.
            var throwawayKey = new byte[16];
            RandomNumberGenerator.Fill(throwawayKey);

            var header = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Ordinary,
                Nonce = nonce,
                AuthData = _localNodeId
            };
            var rawHeader = Discv5Packet.BuildRawHeader(header);
            var aad = Discv5Packet.BuildAad(maskingIv, rawHeader);
            var encrypted = Discv5Packet.EncryptMessage(throwawayKey, nonce, aad, firstMessagePlaintext);
            return Discv5Packet.EncodePacket(maskingIv, header, remoteNodeId, encrypted);
        }

        private IncomingPacket ProcessOrdinary(
            byte[] maskingIv,
            Discv5Packet.Header header,
            byte[] encMsg,
            byte[] rawHeaderForAad,
            IPEndPoint fromAddr)
        {
            // Ordinary authdata = 32-byte src-id.
            if (header.AuthData == null || header.AuthData.Length != 32)
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
            var srcId = header.AuthData;
            var sessionKey = SessionKey(srcId, fromAddr);

            // If we already have a session try to decrypt against it.
            if (_sessions.TryGetValue(sessionKey, out var session))
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
                    // Fall through to issue a fresh WHOAREYOU — peer may have rotated keys.
                    _sessions.TryRemove(sessionKey, out _);
                }
            }

            // No active session — issue a WHOAREYOU challenge.
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
            catch (Exception) { return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr }; }

            var srcId = auth.SrcId;
            var pendingKey = SessionKey(srcId, fromAddr);
            if (!_pendingChallenges.TryRemove(pendingKey, out var pending))
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };

            // Pull the peer's static secp256k1 pubkey out of the ENR they sent us.
            // The ENR's own signature MUST be verified before we trust its
            // secp256k1 entry — otherwise a peer could spoof another node's
            // identity by sending an ENR carrying that node's pubkey. The
            // signed nodeId must also match the srcId carried in the
            // handshake authdata so the id-signature path can't be replayed.
            byte[] staticPubKey = null;
            if (auth.Record != null && auth.Record.Length > 0)
            {
                try
                {
                    var enr = EnrRecordEncoder.Decode(auth.Record);
                    if (!EnrRecordSigner.Verify(enr))
                        return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
                    var enrNodeId = Discv5Crypto.ComputeNodeId(enr.Secp256k1);
                    if (!ByteUtil.AreEqual(enrNodeId, srcId))
                        return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
                    staticPubKey = enr.Secp256k1;
                }
                catch (Exception) { /* malformed ENR — staticPubKey stays null, we drop below */ }
            }
            if (staticPubKey == null)
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };

            // Verify the id-signature: srcId's static key signed (challenge || ephem-pubkey || ourId).
            var idSigInputHash = Discv5KeyDerivation.ComputeIdSignatureInput(
                pending.ChallengeData,
                auth.EphemeralPubKey,
                _localNodeId);
            if (!Discv5Crypto.VerifyIdSignature(auth.IdSignature, idSigInputHash, staticPubKey))
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };

            // Derive session keys via ECDH(local-static-key, peer-ephem-pubkey) + HKDF.
            byte[] sharedSecret;
            try { sharedSecret = Discv5Crypto.EcdhCompressed(_localKey, auth.EphemeralPubKey); }
            catch (Exception) { return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr }; }
            var (initKey, recpKey) = Discv5KeyDerivation.DeriveSessionKeys(
                sharedSecret, pending.ChallengeData, srcId, _localNodeId);

            var session = new Discv5Session
            {
                RemoteNodeId = srcId,
                RemoteAddr = fromAddr,
                InitiatorKey = initKey,
                RecipientKey = recpKey,
                IsInitiator = false
            };

            // The handshake packet carries the original message encrypted with the new session key.
            byte[] plaintext;
            try
            {
                var aad = Discv5Packet.BuildAad(maskingIv, rawHeaderForAad);
                plaintext = Discv5Packet.DecryptMessage(initKey, header.Nonce, aad, encMsg);
            }
            catch (CryptographicException)
            {
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
            }

            EvictOldestSessionIfAtCap();
            _sessions[SessionKey(srcId, fromAddr)] = session;
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

        /// <summary>
        /// We sent an unauthenticated ordinary packet to a peer and they replied
        /// with a WHOAREYOU challenge. Pull the pending plaintext we stashed,
        /// derive the session keys via ECDH(local-static, peer-static) + HKDF,
        /// build a handshake packet containing the id-signature + ephemeral
        /// pubkey + ENR + the re-encrypted plaintext, store the session, and
        /// return the packet for the listener to send.
        /// </summary>
        private IncomingPacket ProcessWhoAreYou(
            byte[] maskingIv,
            Discv5Packet.Header header,
            byte[] rawHeaderForAad,
            IPEndPoint fromAddr)
        {
            // WHOAREYOU authdata carries no src-id, so we look up the pending
            // outbound by the remote endpoint alone. Most cases have at most one
            // pending outbound per (nodeId, endpoint); if the address is shared
            // by multiple pending node ids we pick the first match.
            string pendingKey = null;
            PendingOutbound pending = null;
            byte[] remoteNodeId = null;
            foreach (var kvp in _pendingOutbound)
            {
                if (kvp.Key.EndsWith("|" + fromAddr.ToString(), StringComparison.Ordinal))
                {
                    pendingKey = kvp.Key;
                    pending = kvp.Value;
                    var pipe = kvp.Key.IndexOf('|');
                    remoteNodeId = kvp.Key.Substring(0, pipe).HexToByteArray();
                    break;
                }
            }
            if (pending == null)
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };

            Discv5HandshakePackets.WhoAreYouAuth who;
            try { who = Discv5HandshakePackets.WhoAreYouAuth.Decode(header.AuthData); }
            catch (Exception) { return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr }; }

            // Generate ephemeral secp256k1 key for ECDH.
            var ephem = EthECKey.GenerateKey();
            var ephemPubCompressed = ephem.GetPubKey(compresseed: true);

            // challenge-data = masking-iv || raw-header — reproduce exactly what the peer
            // hashed so HKDF derives matching session keys on both sides.
            var challengeData = Discv5Packet.BuildAad(maskingIv, rawHeaderForAad);

            // id-signature input: sha256("discovery v5 identity proof" || challenge-data
            //                            || ephem-pubkey-compressed || node-B-id)
            var idSigInput = Discv5KeyDerivation.ComputeIdSignatureInput(
                challengeData, ephemPubCompressed, remoteNodeId);
            byte[] idSig;
            try { idSig = Discv5Crypto.SignIdSignature(_localKey, idSigInput); }
            catch (Exception)
            {
                _pendingOutbound.TryRemove(pendingKey, out _);
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
            }

            byte[] sharedSecret;
            try { sharedSecret = Discv5Crypto.EcdhCompressed(ephem, pending.PeerStaticPubKey); }
            catch (Exception)
            {
                _pendingOutbound.TryRemove(pendingKey, out _);
                return new IncomingPacket { Kind = IncomingPacketKind.Ignored, Source = fromAddr };
            }

            // Initiator side: derive with (localNodeId /*nodeA*/, remoteNodeId /*nodeB*/).
            var (initKey, recpKey) = Discv5KeyDerivation.DeriveSessionKeys(
                sharedSecret, challengeData, _localNodeId, remoteNodeId);

            // Include our ENR only if the peer's view of our seq is stale (0 = unknown).
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

            // Re-encrypt the original plaintext under the new session key with a fresh
            // nonce + masking iv. We are the initiator → encrypt with InitiatorKey.
            var outNonce = new byte[Discv5Packet.NonceLength];
            RandomNumberGenerator.Fill(outNonce);
            var outMaskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(outMaskingIv);

            var outHeader = new Discv5Packet.Header
            {
                Flag = Discv5Packet.PacketFlag.Handshake,
                Nonce = outNonce,
                AuthData = handshakeAuth.Encode()
            };
            var outRawHeader = Discv5Packet.BuildRawHeader(outHeader);
            var outAad = Discv5Packet.BuildAad(outMaskingIv, outRawHeader);
            var encryptedMsg = Discv5Packet.EncryptMessage(initKey, outNonce, outAad, pending.MessagePlaintext);
            var outPacket = Discv5Packet.EncodePacket(outMaskingIv, outHeader, remoteNodeId, encryptedMsg);

            var session = new Discv5Session
            {
                RemoteNodeId = remoteNodeId,
                RemoteAddr = fromAddr,
                InitiatorKey = initKey,
                RecipientKey = recpKey,
                IsInitiator = true
            };
            EvictOldestSessionIfAtCap();
            _sessions[SessionKey(remoteNodeId, fromAddr)] = session;
            _pendingOutbound.TryRemove(pendingKey, out _);

            OutboundSessionEstablished?.Invoke(this, new SessionEstablishedEventArgs
            {
                Session = session,
                EnrEncoded = record
            });

            return new IncomingPacket
            {
                Kind = IncomingPacketKind.NeedWhoAreYou,   // listener sends OutgoingBytes
                OutgoingBytes = outPacket,
                Source = fromAddr
            };
        }

        private byte[] BuildWhoAreYou(byte[] srcId, IPEndPoint fromAddr, byte[] originalNonce)
        {
            var key = SessionKey(srcId, fromAddr);

            // Retransmission tie-breaker: a UDP-layer retransmission of the
            // SAME ordinary packet (matching nonce) within the handshake
            // window must echo the SAME challenge bytes so the peer's
            // in-flight handshake reply — whose HKDF salt embeds the
            // original challenge data — stays valid. A genuinely-new
            // ordinary packet (different nonce) still gets a fresh challenge
            // so the WHOAREYOU header echoes the latest triggering nonce per
            // discv5-wire.md §"WHOAREYOU".
            if (_pendingChallenges.TryGetValue(key, out var existing)
                && existing.EncodedPacket != null
                && existing.OriginalNonce != null
                && originalNonce != null
                && Nethereum.Util.ByteUtil.AreEqual(existing.OriginalNonce, originalNonce)
                && DateTime.UtcNow - existing.CreatedUtc <= HandshakeTimeout)
            {
                return existing.EncodedPacket;
            }

            var idNonce = new byte[Discv5HandshakePackets.WhoAreYouAuth.IdNonceLength];
            RandomNumberGenerator.Fill(idNonce);

            // enr-seq = 0 means "I don't know your ENR" — peer should include their ENR in the handshake reply.
            ulong enrSeq = 0;
            var authdata = new Discv5HandshakePackets.WhoAreYouAuth { IdNonce = idNonce, EnrSeq = enrSeq }.Encode();

            var maskingIv = new byte[Discv5Packet.MaskingIvLength];
            RandomNumberGenerator.Fill(maskingIv);

            var header = new Discv5Packet.Header
            {
                // WHOAREYOU spec: Nonce field echoes the nonce of the packet that triggered the challenge.
                Flag = Discv5Packet.PacketFlag.WhoAreYou,
                Nonce = originalNonce ?? new byte[Discv5Packet.NonceLength],
                AuthData = authdata
            };
            var rawHeader = Discv5Packet.BuildRawHeader(header);

            // challenge-data = masking-iv || raw-header — both sides must agree on these bytes
            // for the HKDF salt to derive matching session keys.
            var challengeData = Discv5Packet.BuildAad(maskingIv, rawHeader);
            var encoded = Discv5Packet.EncodePacket(maskingIv, header, srcId, Array.Empty<byte>());

            _pendingChallenges[key] = new Discv5PendingChallenge
            {
                IdNonce = idNonce,
                ChallengeData = challengeData,
                EnrSeq = enrSeq,
                OriginalNonce = originalNonce,
                EncodedPacket = encoded
            };
            return encoded;
        }

        private static string SessionKey(byte[] nodeId, IPEndPoint addr)
            => $"{nodeId.ToHex()}|{addr}";

        /// <summary>Current count of in-flight WHOAREYOU challenges. Test/diagnostic surface.</summary>
        public int PendingChallengeCount => _pendingChallenges.Count;

        /// <summary>Current count of established sessions. Test/diagnostic surface.</summary>
        public int SessionCount => _sessions.Count;

        private void EvictOldestSessionIfAtCap()
        {
            if (_sessions.Count < MaxSessions) return;
            string oldestKey = null;
            DateTime oldestTime = DateTime.MaxValue;
            foreach (var kvp in _sessions)
            {
                if (kvp.Value.CreatedUtc < oldestTime)
                {
                    oldestTime = kvp.Value.CreatedUtc;
                    oldestKey = kvp.Key;
                }
            }
            if (oldestKey != null)
                _sessions.TryRemove(oldestKey, out _);
        }

        private void MaybeSweepStaleChallenges()
        {
            var now = DateTime.UtcNow;
            if (now - _lastHandshakeGcUtc < HandshakeGcInterval) return;
            lock (_gcLock)
            {
                if (now - _lastHandshakeGcUtc < HandshakeGcInterval) return;
                _lastHandshakeGcUtc = now;
            }
            SweepStaleChallenges(now);
        }

        /// <summary>
        /// Evict any pending WHOAREYOU challenges older than
        /// <see cref="HandshakeTimeout"/>. Exposed for tests so the GC can be
        /// driven deterministically without waiting for the next packet to
        /// arrive.
        /// </summary>
        public void SweepStaleChallenges()
            => SweepStaleChallenges(DateTime.UtcNow);

        private void SweepStaleChallenges(DateTime now)
        {
            foreach (var kvp in _pendingChallenges)
            {
                if (now - kvp.Value.CreatedUtc > HandshakeTimeout)
                {
                    _pendingChallenges.TryRemove(kvp.Key, out _);
                }
            }
            // Same retention for _pendingOutbound: any initial packet we
            // sent that never received a WHOAREYOU reply within HandshakeTimeout
            // is dead. Geth's discover/v5wire/session.go expires unanswered
            // session attempts on the same cadence.
            foreach (var kvp in _pendingOutbound)
            {
                if (now - kvp.Value.CreatedUtc > HandshakeTimeout)
                {
                    _pendingOutbound.TryRemove(kvp.Key, out _);
                }
            }
        }
    }
}
