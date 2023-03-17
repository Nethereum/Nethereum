using System.Threading.Tasks;
using Nethereum.Contracts.ContractHandlers;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts.Standards.ENS
{
    public partial class OffchainResolverService
    {

        public const string ENSIP_10_INTERFACEID = "0x9061b923";
#if !DOTNET35

        public Task<bool> SupportsENSIP10InterfaceQueryAsync()
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceID = ENSIP_10_INTERFACEID.HexToByteArray();
            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction);
        }

#endif
    }
}
