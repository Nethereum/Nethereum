using System.Numerics;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.Interfaces;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer.Bls;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Aggregation
{
    public class BlsAggregator : IAggregator
    {
        public const int BLS_SIGNATURE_SIZE = 96;
        public const int BLS_PUBKEY_SIZE = 48;
        public const int COMBINED_SIZE = BLS_SIGNATURE_SIZE + BLS_PUBKEY_SIZE;

        private readonly IBls _bls;
        private readonly IWeb3 _web3;
        private readonly string _entryPointAddress;
        private readonly BigInteger _chainId;

        public string AggregatorAddress { get; }

        public BlsAggregator(
            IBls bls,
            IWeb3 web3,
            string entryPointAddress,
            string aggregatorAddress,
            BigInteger chainId)
        {
            _bls = bls ?? throw new ArgumentNullException(nameof(bls));
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _entryPointAddress = entryPointAddress ?? throw new ArgumentNullException(nameof(entryPointAddress));
            AggregatorAddress = aggregatorAddress ?? throw new ArgumentNullException(nameof(aggregatorAddress));
            _chainId = chainId;
        }

        public async Task<byte[]> AggregateSignaturesAsync(PackedUserOperation[] userOps)
        {
            if (userOps == null || userOps.Length == 0)
            {
                throw new ArgumentException("At least one user operation is required.", nameof(userOps));
            }

            var signatures = new List<byte[]>();
            var publicKeys = new List<byte[]>();

            foreach (var userOp in userOps)
            {
                var sig = userOp.Signature;
                if (sig == null || sig.Length < COMBINED_SIZE)
                {
                    throw new InvalidOperationException(
                        $"UserOperation signature must be at least {COMBINED_SIZE} bytes " +
                        $"(96 for BLS signature + 48 for public key)");
                }

                var (signature, publicKey) = _bls.ExtractSignatureAndPublicKey(sig);
                signatures.Add(signature);
                publicKeys.Add(publicKey);
            }

            var aggregatedSignature = _bls.AggregateSignatures(signatures.ToArray());

            var result = new byte[aggregatedSignature.Length + publicKeys.Sum(pk => pk.Length)];
            int offset = 0;

            Buffer.BlockCopy(aggregatedSignature, 0, result, offset, aggregatedSignature.Length);
            offset += aggregatedSignature.Length;

            foreach (var pk in publicKeys)
            {
                Buffer.BlockCopy(pk, 0, result, offset, pk.Length);
                offset += pk.Length;
            }

            return await Task.FromResult(result);
        }

        public async Task ValidateSignaturesAsync(PackedUserOperation[] userOps, byte[] aggregatedSignature)
        {
            if (userOps == null || userOps.Length == 0)
            {
                throw new ArgumentException("At least one user operation is required.", nameof(userOps));
            }

            if (aggregatedSignature == null || aggregatedSignature.Length == 0)
            {
                throw new ArgumentException("Aggregated signature is required.", nameof(aggregatedSignature));
            }

            var entryPointService = new EntryPointService(_web3, _entryPointAddress);

            var messages = new List<byte[]>();
            var publicKeys = new List<byte[]>();

            int expectedPubKeysSize = userOps.Length * BLS_PUBKEY_SIZE;
            int sigSize = aggregatedSignature.Length - expectedPubKeysSize;

            if (sigSize != BLS_SIGNATURE_SIZE)
            {
                throw new InvalidOperationException(
                    $"Invalid aggregated signature format. Expected {BLS_SIGNATURE_SIZE} byte signature + " +
                    $"{expectedPubKeysSize} bytes for {userOps.Length} public keys.");
            }

            var signature = new byte[BLS_SIGNATURE_SIZE];
            Buffer.BlockCopy(aggregatedSignature, 0, signature, 0, BLS_SIGNATURE_SIZE);

            for (int i = 0; i < userOps.Length; i++)
            {
                var userOpHash = await entryPointService.GetUserOpHashQueryAsync(userOps[i]);
                messages.Add(userOpHash);

                var pk = new byte[BLS_PUBKEY_SIZE];
                Buffer.BlockCopy(aggregatedSignature, BLS_SIGNATURE_SIZE + (i * BLS_PUBKEY_SIZE), pk, 0, BLS_PUBKEY_SIZE);
                publicKeys.Add(pk);
            }

            var isValid = _bls.VerifyAggregate(
                signature,
                publicKeys.ToArray(),
                messages.ToArray(),
                new byte[32]);

            if (!isValid)
            {
                throw new InvalidOperationException("BLS aggregate signature verification failed");
            }
        }

        public async Task<byte[]> ValidateUserOpSignatureAsync(PackedUserOperation userOp)
        {
            if (userOp == null)
            {
                throw new ArgumentNullException(nameof(userOp));
            }

            var sig = userOp.Signature;
            if (sig == null || sig.Length < COMBINED_SIZE)
            {
                throw new InvalidOperationException(
                    $"UserOperation signature must be at least {COMBINED_SIZE} bytes");
            }

            var (signature, publicKey) = _bls.ExtractSignatureAndPublicKey(sig);

            var entryPointService = new EntryPointService(_web3, _entryPointAddress);
            var userOpHash = await entryPointService.GetUserOpHashQueryAsync(userOp);

            var isValid = _bls.Verify(signature, publicKey, userOpHash);

            if (!isValid)
            {
                throw new InvalidOperationException("BLS signature verification failed for user operation");
            }

            return publicKey;
        }

        public static bool IsBlsSignature(byte[]? signature)
        {
            return signature != null && signature.Length >= COMBINED_SIZE;
        }

        public static (byte[] Signature, byte[] PublicKey) ParseSignature(byte[] combinedSignature)
        {
            if (combinedSignature == null || combinedSignature.Length < COMBINED_SIZE)
            {
                throw new ArgumentException(
                    $"Combined signature must be at least {COMBINED_SIZE} bytes",
                    nameof(combinedSignature));
            }

            var signature = new byte[BLS_SIGNATURE_SIZE];
            var publicKey = new byte[BLS_PUBKEY_SIZE];

            Buffer.BlockCopy(combinedSignature, 0, signature, 0, BLS_SIGNATURE_SIZE);
            Buffer.BlockCopy(combinedSignature, BLS_SIGNATURE_SIZE, publicKey, 0, BLS_PUBKEY_SIZE);

            return (signature, publicKey);
        }
    }
}
