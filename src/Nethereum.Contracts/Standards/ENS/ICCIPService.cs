using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using System.Threading.Tasks;

namespace Nethereum.Contracts.Standards.ENS
{
    public interface IEnsCCIPService
    {
        Task<byte[]> ResolveCCIPRead(OffchainResolverService offchainResolver, OffchainLookupError offchainLookup, int maxLookupRedirects);
    }
}