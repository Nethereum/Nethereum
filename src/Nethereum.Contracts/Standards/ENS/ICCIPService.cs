using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using System.Threading.Tasks;

namespace Nethereum.Contracts.Standards.ENS
{
    public interface IEnsCCIPService
    {
        Task<byte[]> ResolveCCIPRead(OffchainResolverService offchainResolver, OffchainLookupError offchainLookup, int maxLookupRedirects);

        /// <summary>
        /// Generic EIP-3668 CCIP-Read handler: fetches from the offchain gateway, then calls back the
        /// sender's callbackFunction(response, extraData) via eth_call, following nested OffchainLookup
        /// reverts up to maxLookupRedirects. Used by the Universal Resolver, whose resolve/reverse calls
        /// raise OffchainLookup directly (the callback is not the legacy resolveWithProof).
        /// </summary>
        Task<byte[]> ResolveCCIPReadGenericAsync(IEthApiContractService ethApiContractService, OffchainLookupError offchainLookup, int maxLookupRedirects);
    }
}