using System;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ChainStateVerification
{
    public class StorageProofVerifier : IStorageProofVerifier
    {
        private readonly ITrustedHeaderProvider _headerProvider;
        private readonly IEthGetProof _getProof;
        private readonly ITrieProofVerifier _proofVerifier;

        public StorageProofVerifier(
            ITrustedHeaderProvider headerProvider,
            IEthGetProof getProof,
            ITrieProofVerifier proofVerifier)
        {
            _headerProvider = headerProvider ?? throw new ArgumentNullException(nameof(headerProvider));
            _getProof = getProof ?? throw new ArgumentNullException(nameof(getProof));
            _proofVerifier = proofVerifier ?? throw new ArgumentNullException(nameof(proofVerifier));
        }

        public async Task<byte[]> GetStorageValueAsync(string address, string storageSlotHex)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }
            if (string.IsNullOrWhiteSpace(storageSlotHex))
            {
                throw new ArgumentException("Storage slot is required.", nameof(storageSlotHex));
            }

            var trustedHeader = _headerProvider.GetLatestFinalized();
            var blockParameter = new BlockParameter(trustedHeader.BlockNumber);
            var proof = await _getProof
                .SendRequestAsync(address.EnsureHexPrefix(), new[] { storageSlotHex.EnsureHexPrefix() }, blockParameter)
                .ConfigureAwait(false);

            if (proof == null)
            {
                throw new InvalidOperationException("RPC node did not return a storage proof.");
            }

            var account = _proofVerifier.VerifyAccountProof(trustedHeader.StateRoot, proof);
            var storageEntry = proof.StorageProof.FirstOrDefault();
            if (storageEntry == null)
            {
                throw new InvalidOperationException("RPC proof did not include the requested storage slot.");
            }

            return _proofVerifier.VerifyStorageProof(account, storageEntry);
        }
    }
}
