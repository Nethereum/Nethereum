using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;

namespace Nethereum.AccountAbstraction.SessionKeys
{
    public class SessionKeyManager
    {
        private readonly Dictionary<string, SessionKeyEntry> _keys = new();
        private readonly ISessionKeyStore _store;

        public SessionKeyManager(ISessionKeyStore? store = null)
        {
            _store = store ?? new InMemorySessionKeyStore();
        }

        public async Task<GeneratedSessionKey> GenerateSessionKeyAsync(
            string accountAddress,
            int validDays = 30)
        {
            var ecKey = EthECKey.GenerateKey();
            var address = ecKey.GetPublicAddress();
            var privateKey = ecKey.GetPrivateKey();

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();
            var validUntil = now + (ulong)(validDays * 24 * 60 * 60);

            var sessionKey = new GeneratedSessionKey
            {
                Key = address,
                PrivateKey = privateKey,
                AccountAddress = accountAddress,
                ValidAfter = now,
                ValidUntil = validUntil,
                GeneratedAt = DateTimeOffset.UtcNow
            };

            var entry = new SessionKeyEntry
            {
                Key = address,
                PrivateKey = privateKey,
                AccountAddress = accountAddress,
                ValidAfter = now,
                ValidUntil = validUntil,
                IsActive = false,
                RegisteredAt = DateTimeOffset.UtcNow
            };

            _keys[address.ToLowerInvariant()] = entry;
            await _store.SaveAsync(entry);

            return sessionKey;
        }

        public async Task<SessionKeyEntry?> GetSessionKeyAsync(string keyAddress)
        {
            var normalized = keyAddress.ToLowerInvariant();

            if (_keys.TryGetValue(normalized, out var entry))
            {
                return entry;
            }

            entry = await _store.LoadAsync(keyAddress);
            if (entry != null)
            {
                _keys[normalized] = entry;
            }

            return entry;
        }

        public async Task<SessionKeyEntry[]> GetSessionKeysForAccountAsync(string accountAddress)
        {
            var all = await _store.LoadAllAsync();
            return all.Where(e => e.AccountAddress.Equals(accountAddress, StringComparison.OrdinalIgnoreCase)).ToArray();
        }

        public async Task MarkRegisteredAsync(string keyAddress)
        {
            var entry = await GetSessionKeyAsync(keyAddress);
            if (entry != null)
            {
                entry.IsActive = true;
                await _store.SaveAsync(entry);
            }
        }

        public async Task<byte[]> SignUserOpHashAsync(string keyAddress, byte[] userOpHash)
        {
            var entry = await GetSessionKeyAsync(keyAddress);
            if (entry == null)
            {
                throw new ArgumentException($"Session key not found: {keyAddress}");
            }

            var ecKey = new EthECKey(entry.PrivateKey);
            var signature = ecKey.SignAndCalculateV(userOpHash);

            return EthECDSASignature.CreateStringSignature(signature).HexToByteArray();
        }

        public async Task<PackedUserOperation> SignUserOperationAsync(
            PackedUserOperation userOp,
            string keyAddress,
            string entryPoint,
            BigInteger chainId)
        {
            var hash = UserOperationBuilder.HashUserOperation(userOp, entryPoint, chainId);
            var signature = await SignUserOpHashAsync(keyAddress, hash);

            return new PackedUserOperation
            {
                Sender = userOp.Sender,
                Nonce = userOp.Nonce,
                InitCode = userOp.InitCode,
                CallData = userOp.CallData,
                AccountGasLimits = userOp.AccountGasLimits,
                PreVerificationGas = userOp.PreVerificationGas,
                GasFees = userOp.GasFees,
                PaymasterAndData = userOp.PaymasterAndData,
                Signature = signature
            };
        }

        public async Task RemoveSessionKeyAsync(string keyAddress)
        {
            var normalized = keyAddress.ToLowerInvariant();
            _keys.Remove(normalized);
            await _store.DeleteAsync(keyAddress);
        }

        public async Task<SessionKeyEntry?> GetBestSessionKeyAsync(string accountAddress)
        {
            var keys = await GetSessionKeysForAccountAsync(accountAddress);
            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            return keys
                .Where(k => k.IsActive && k.ValidAfter <= now && k.ValidUntil > now)
                .OrderByDescending(k => k.ValidUntil)
                .FirstOrDefault();
        }
    }
}
