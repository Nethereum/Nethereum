using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ChainStateVerification.Caching;
using Nethereum.Consensus.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.ChainStateVerification
{
    public class VerifiedStateService : IVerifiedStateService, IDisposable
    {
        private static readonly byte[] EmptyCodeHash = "0xc5d2460186f7233c927e7db2dcc703c0e500b653ca82273b7bfad8045d85a470".HexToByteArray();

        private readonly ITrustedHeaderProvider _headerProvider;
        private readonly IEthGetProof _getProof;
        private readonly IEthGetCode _getCode;
        private readonly ITrieProofVerifier _proofVerifier;
        private readonly VerifiedStateCache _cache;
        private readonly Sha3Keccack _sha3 = new Sha3Keccack();
        private bool _disposed;

        public VerificationMode Mode { get; set; } = VerificationMode.Finalized;
        public bool EnableCaching { get; set; } = true;
        public bool VerifyCodeHash { get; set; } = true;

        public VerifiedStateService(
            ITrustedHeaderProvider headerProvider,
            IEthGetProof getProof,
            IEthGetCode getCode,
            ITrieProofVerifier proofVerifier)
            : this(headerProvider, getProof, getCode, proofVerifier, new VerifiedStateCache())
        {
        }

        public VerifiedStateService(
            ITrustedHeaderProvider headerProvider,
            IEthGetProof getProof,
            IEthGetCode getCode,
            ITrieProofVerifier proofVerifier,
            VerifiedStateCache cache)
        {
            _headerProvider = headerProvider ?? throw new ArgumentNullException(nameof(headerProvider));
            _getProof = getProof ?? throw new ArgumentNullException(nameof(getProof));
            _getCode = getCode ?? throw new ArgumentNullException(nameof(getCode));
            _proofVerifier = proofVerifier ?? throw new ArgumentNullException(nameof(proofVerifier));
            _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        }

        public TrustedExecutionHeader GetCurrentHeader()
        {
            return Mode == VerificationMode.Optimistic
                ? _headerProvider.GetLatestOptimistic()
                : _headerProvider.GetLatestFinalized();
        }

        public async Task<Account> GetAccountAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }

            var header = GetCurrentHeader();

            if (EnableCaching)
            {
                _cache.SetBlock(header.BlockNumber, header.StateRoot);

                if (_cache.TryGetAccount(address, out var cachedState))
                {
                    return cachedState.Account;
                }
            }

            var blockParameter = new BlockParameter(header.BlockNumber);
            var proof = await _getProof.SendRequestAsync(address.EnsureHexPrefix(), Array.Empty<string>(), blockParameter).ConfigureAwait(false);

            if (proof == null)
            {
                throw new InvalidOperationException("RPC node did not return an account proof.");
            }

            var account = _proofVerifier.VerifyAccountProof(header.StateRoot, proof);

            if (EnableCaching)
            {
                _cache.SetAccount(address, account);
            }

            return account;
        }

        public async Task<BigInteger> GetBalanceAsync(string address)
        {
            var account = await GetAccountAsync(address).ConfigureAwait(false);
            return account.Balance;
        }

        public async Task<BigInteger> GetNonceAsync(string address)
        {
            var account = await GetAccountAsync(address).ConfigureAwait(false);
            return account.Nonce;
        }

        public async Task<byte[]> GetCodeHashAsync(string address)
        {
            var account = await GetAccountAsync(address).ConfigureAwait(false);
            return account.CodeHash;
        }

        public async Task<byte[]> GetCodeAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }

            var header = GetCurrentHeader();

            if (EnableCaching)
            {
                _cache.SetBlock(header.BlockNumber, header.StateRoot);

                if (_cache.TryGetCode(address, out var cachedCode))
                {
                    return cachedCode;
                }
            }

            var account = await GetAccountAsync(address).ConfigureAwait(false);

            if (account.CodeHash == null || account.CodeHash.SequenceEqual(EmptyCodeHash))
            {
                if (EnableCaching)
                {
                    _cache.SetCode(address, Array.Empty<byte>());
                }
                return Array.Empty<byte>();
            }

            var blockParameter = new BlockParameter(header.BlockNumber);
            var codeHex = await _getCode.SendRequestAsync(address.EnsureHexPrefix(), blockParameter).ConfigureAwait(false);
            var code = codeHex?.HexToByteArray() ?? Array.Empty<byte>();

            if (VerifyCodeHash && code.Length > 0)
            {
                var computedHash = _sha3.CalculateHash(code);
                if (!computedHash.SequenceEqual(account.CodeHash))
                {
                    throw new InvalidChainDataException(
                        $"Code hash mismatch for {address}. Expected: {account.CodeHash.ToHex(true)}, Got: {computedHash.ToHex(true)}. RPC may have returned tampered code.");
                }
            }

            if (EnableCaching)
            {
                _cache.SetCode(address, code);
            }

            return code;
        }

        public async Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
        {
            var slotHex = position.ToHex(true).EnsureHexPrefix();
            if (slotHex.Length % 2 != 0)
            {
                slotHex = "0x0" + slotHex.Substring(2);
            }
            return await GetStorageAtAsync(address, slotHex).ConfigureAwait(false);
        }

        public async Task<byte[]> GetStorageAtAsync(string address, string slotHex)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }
            if (string.IsNullOrWhiteSpace(slotHex))
            {
                throw new ArgumentException("Storage slot is required.", nameof(slotHex));
            }

            var normalizedSlot = slotHex.EnsureHexPrefix();
            var header = GetCurrentHeader();

            if (EnableCaching)
            {
                _cache.SetBlock(header.BlockNumber, header.StateRoot);

                if (_cache.TryGetStorage(address, normalizedSlot, out var cachedStorage))
                {
                    return cachedStorage;
                }
            }

            var blockParameter = new BlockParameter(header.BlockNumber);
            var proof = await _getProof.SendRequestAsync(
                address.EnsureHexPrefix(),
                new[] { normalizedSlot },
                blockParameter).ConfigureAwait(false);

            if (proof == null)
            {
                throw new InvalidOperationException("RPC node did not return a storage proof.");
            }

            var account = _proofVerifier.VerifyAccountProof(header.StateRoot, proof);

            if (EnableCaching)
            {
                _cache.SetAccount(address, account);
            }

            var storageEntry = proof.StorageProof?.FirstOrDefault();

            if (storageEntry == null)
            {
                throw new InvalidOperationException("RPC proof did not include the requested storage slot.");
            }

            var storageValue = _proofVerifier.VerifyStorageProof(account, storageEntry);

            if (EnableCaching)
            {
                _cache.SetStorage(address, normalizedSlot, storageValue);
            }

            return storageValue;
        }

        public byte[] GetBlockHash(ulong blockNumber)
        {
            return _headerProvider.GetBlockHash(blockNumber);
        }

        public void ClearCache()
        {
            _cache.Clear();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _cache?.Dispose();
            }

            _disposed = true;
        }
    }
}
