using System;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.BlockchainProcessing.BlockStorage.Entities;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.Contracts.Services;

namespace Nethereum.BlockchainProcessing.Services
{
    public static class ChainStateValidationService
    {
        public static async Task EnsureChainIdMatchesAsync(
            IEthApiContractService ethApiContractService,
            IChainStateRepositoryFactory repositoryFactory,
            CancellationToken cancellationToken = default)
        {
            if (ethApiContractService == null) throw new ArgumentNullException(nameof(ethApiContractService));
            if (repositoryFactory == null) throw new ArgumentNullException(nameof(repositoryFactory));

            var chainIdHex = await ethApiContractService.ChainId.SendRequestAsync().ConfigureAwait(false);
            if (chainIdHex == null)
            {
                throw new InvalidOperationException("Unable to read ChainId from the RPC endpoint.");
            }

            var chainId = (int)chainIdHex.Value;
            var chainStateRepository = repositoryFactory.CreateChainStateRepository();
            var existingState = await chainStateRepository.GetChainStateAsync().ConfigureAwait(false);

            if (existingState == null)
            {
                var newState = new ChainState
                {
                    ChainId = chainId
                };
                await chainStateRepository.UpsertChainStateAsync(newState).ConfigureAwait(false);
                return;
            }

            if (existingState.ChainId != chainId)
            {
                throw new InvalidOperationException(
                    $"ChainId mismatch. Database ChainId={existingState.ChainId}, RPC ChainId={chainId}.");
            }
        }
    }
}
