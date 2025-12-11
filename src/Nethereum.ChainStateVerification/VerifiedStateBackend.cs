using System;
using System.Threading.Tasks;
using Nethereum.Consensus.LightClient;
using Nethereum.Model;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.ChainStateVerification
{
    public class VerifiedStateBackend : IVerifiedStateBackend
    {
        private readonly ITrustedHeaderProvider _headerProvider;
        private readonly IEthGetProof _getProof;
        private readonly ITrieProofVerifier _proofVerifier;

        public VerifiedStateBackend(
            ITrustedHeaderProvider headerProvider,
            IEthGetProof getProof,
            ITrieProofVerifier proofVerifier)
        {
            _headerProvider = headerProvider ?? throw new ArgumentNullException(nameof(headerProvider));
            _getProof = getProof ?? throw new ArgumentNullException(nameof(getProof));
            _proofVerifier = proofVerifier ?? throw new ArgumentNullException(nameof(proofVerifier));
        }

        public async Task<Account> GetAccountAsync(string address)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                throw new ArgumentException("Address is required.", nameof(address));
            }

            var trustedHeader = _headerProvider.GetLatestFinalized();
            var blockParameter = new BlockParameter(trustedHeader.BlockNumber);
            var proof = await _getProof.SendRequestAsync(address, Array.Empty<string>(), blockParameter).ConfigureAwait(false);
            if (proof == null)
            {
                throw new InvalidOperationException("RPC node did not return an account proof.");
            }

            return _proofVerifier.VerifyAccountProof(trustedHeader.StateRoot, proof);
        }
    }
}
