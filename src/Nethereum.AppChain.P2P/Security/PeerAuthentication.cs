using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Nethereum.Signer;

namespace Nethereum.AppChain.P2P.Security
{
    public class PeerAuthenticator
    {
        private readonly PeerAuthConfig _config;
        private readonly ILogger<PeerAuthenticator>? _logger;
        private readonly ConcurrentDictionary<string, AuthenticatedPeer> _authenticatedPeers = new();
        private readonly ConcurrentDictionary<string, (ulong Nonce, DateTimeOffset Timestamp)> _usedNonces = new();

        public PeerAuthenticator(PeerAuthConfig? config = null, ILogger<PeerAuthenticator>? logger = null)
        {
            _config = config ?? PeerAuthConfig.Default;
            _logger = logger;
        }

        public AuthResult Authenticate(string peerId, byte[] challenge, byte[] signature)
        {
            try
            {
                var r = new byte[32];
                var s = new byte[32];
                Array.Copy(signature, 0, r, 0, 32);
                Array.Copy(signature, 32, s, 0, 32);

                var recoveredKey = EthECKey.RecoverFromSignature(
                    EthECDSASignatureFactory.FromComponents(r, s, signature[64]),
                    Nethereum.Util.Sha3Keccack.Current.CalculateHash(challenge));

                var recoveredAddress = recoveredKey.GetPublicAddress().ToLowerInvariant();

                if (_config.AllowedPeers.Count > 0 &&
                    !_config.AllowedPeers.Contains(recoveredAddress))
                {
                    return AuthResult.Unauthorized($"Peer {recoveredAddress} not in allowlist");
                }

                _authenticatedPeers[peerId] = new AuthenticatedPeer
                {
                    PeerId = peerId,
                    Address = recoveredAddress,
                    AuthenticatedAt = DateTimeOffset.UtcNow,
                    PublicKey = recoveredKey.GetPubKey()
                };

                _logger?.LogInformation("Peer {PeerId} authenticated as {Address}", peerId, recoveredAddress);
                return AuthResult.Success(recoveredAddress);
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Authentication failed for peer {PeerId}", peerId);
                return AuthResult.InvalidSignature(ex.Message);
            }
        }

        public bool VerifyMessage(string peerId, byte[] messageHash, byte[] signature, ulong nonce, long timestamp)
        {
            if (!_authenticatedPeers.TryGetValue(peerId, out var peer))
            {
                _logger?.LogWarning("Message verification failed: peer {PeerId} not authenticated", peerId);
                return false;
            }

            var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            if (Math.Abs(now - timestamp) > _config.MaxTimestampDriftSeconds)
            {
                _logger?.LogWarning("Message rejected: timestamp drift too large for peer {PeerId}", peerId);
                return false;
            }

            var nonceKey = $"{peerId}:{nonce}";
            if (_usedNonces.ContainsKey(nonceKey))
            {
                _logger?.LogWarning("Message rejected: nonce {Nonce} already used by peer {PeerId}", nonce, peerId);
                return false;
            }

            try
            {
                var r = new byte[32];
                var s = new byte[32];
                Array.Copy(signature, 0, r, 0, 32);
                Array.Copy(signature, 32, s, 0, 32);

                var recoveredKey = EthECKey.RecoverFromSignature(
                    EthECDSASignatureFactory.FromComponents(r, s, signature[64]),
                    messageHash);

                var recoveredAddress = recoveredKey.GetPublicAddress().ToLowerInvariant();
                if (recoveredAddress != peer.Address)
                {
                    _logger?.LogWarning("Message signature mismatch for peer {PeerId}", peerId);
                    return false;
                }

                _usedNonces.TryAdd(nonceKey, (nonce, DateTimeOffset.UtcNow));
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Message signature verification failed for peer {PeerId}", peerId);
                return false;
            }
        }

        public byte[] CreateChallenge()
        {
            var challenge = new byte[32];
            using var rng = System.Security.Cryptography.RandomNumberGenerator.Create();
            rng.GetBytes(challenge);
            return challenge;
        }

        public byte[] SignChallenge(byte[] challenge, EthECKey signerKey)
        {
            var hash = Nethereum.Util.Sha3Keccack.Current.CalculateHash(challenge);
            var signature = signerKey.SignAndCalculateV(hash);

            var sigBytes = new byte[65];
            Array.Copy(signature.R, 0, sigBytes, 0, 32);
            Array.Copy(signature.S, 0, sigBytes, 32, 32);
            sigBytes[64] = (byte)(signature.V[0] - 27);

            return sigBytes;
        }

        public AuthenticatedPeer? GetPeer(string peerId)
        {
            return _authenticatedPeers.TryGetValue(peerId, out var peer) ? peer : null;
        }

        public void RemovePeer(string peerId)
        {
            _authenticatedPeers.TryRemove(peerId, out _);

            var keysToRemove = _usedNonces.Keys.Where(k => k.StartsWith(peerId + ":")).ToList();
            foreach (var key in keysToRemove)
            {
                _usedNonces.TryRemove(key, out _);
            }
        }

        public void CleanupOldNonces(TimeSpan maxAge)
        {
            var cutoff = DateTimeOffset.UtcNow - maxAge;
            var keysToRemove = _usedNonces
                .Where(kv => kv.Value.Timestamp < cutoff)
                .Select(kv => kv.Key)
                .ToList();
            foreach (var key in keysToRemove)
            {
                _usedNonces.TryRemove(key, out _);
            }
        }
    }

    public class PeerAuthConfig
    {
        public HashSet<string> AllowedPeers { get; set; } = new();
        public int MaxTimestampDriftSeconds { get; set; } = 30;
        public bool RequireAuthentication { get; set; } = true;

        public static PeerAuthConfig Default => new()
        {
            AllowedPeers = new HashSet<string>(),
            MaxTimestampDriftSeconds = 30,
            RequireAuthentication = true
        };

        public static PeerAuthConfig OpenAccess => new()
        {
            AllowedPeers = new HashSet<string>(),
            MaxTimestampDriftSeconds = 30,
            RequireAuthentication = false
        };

        public static PeerAuthConfig WithAllowlist(IEnumerable<string> peers) => new()
        {
            AllowedPeers = new HashSet<string>(peers.Select(p => p.ToLowerInvariant())),
            MaxTimestampDriftSeconds = 30,
            RequireAuthentication = true
        };
    }

    public class AuthenticatedPeer
    {
        public string PeerId { get; set; } = "";
        public string Address { get; set; } = "";
        public byte[] PublicKey { get; set; } = Array.Empty<byte>();
        public DateTimeOffset AuthenticatedAt { get; set; }
    }

    public class AuthResult
    {
        public bool IsSuccess { get; }
        public string? Address { get; }
        public string? Error { get; }
        public AuthError ErrorType { get; }

        private AuthResult(bool success, string? address, string? error, AuthError errorType)
        {
            IsSuccess = success;
            Address = address;
            Error = error;
            ErrorType = errorType;
        }

        public static AuthResult Success(string address) => new(true, address, null, AuthError.None);
        public static AuthResult Unauthorized(string error) => new(false, null, error, AuthError.Unauthorized);
        public static AuthResult InvalidSignature(string error) => new(false, null, error, AuthError.InvalidSignature);
    }

    public enum AuthError
    {
        None,
        Unauthorized,
        InvalidSignature
    }
}
