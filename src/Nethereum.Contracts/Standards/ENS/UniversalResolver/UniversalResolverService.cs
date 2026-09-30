using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.UniversalResolver.ContractDefinition;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ENS.UniversalResolver
{
    public partial class UniversalResolverService : ContractServiceBase
    {
        public UniversalResolverService(IEthApiContractService ethApiContractService, string contractAddress)
        {
#if !DOTNET35
            ContractHandler = ethApiContractService.GetContractHandler(contractAddress);
#endif
        }

#if !DOTNET35
        public Task<ResolveOutputDTO> ResolveQueryAsync(ResolveFunction resolveFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ResolveFunction, ResolveOutputDTO>(resolveFunction, blockParameter);
        }

        public Task<ResolveOutputDTO> ResolveQueryAsync(byte[] name, byte[] data, BlockParameter blockParameter = null)
        {
            var resolveFunction = new ResolveFunction();
            resolveFunction.Name = name;
            resolveFunction.Data = data;

            return ContractHandler.QueryDeserializingToObjectAsync<ResolveFunction, ResolveOutputDTO>(resolveFunction, blockParameter);
        }

        public Task<ResolveWithGatewaysOutputDTO> ResolveWithGatewaysQueryAsync(ResolveWithGatewaysFunction resolveWithGatewaysFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ResolveWithGatewaysFunction, ResolveWithGatewaysOutputDTO>(resolveWithGatewaysFunction, blockParameter);
        }

        public Task<ResolveWithGatewaysOutputDTO> ResolveWithGatewaysQueryAsync(byte[] name, byte[] data, List<string> gateways, BlockParameter blockParameter = null)
        {
            var resolveWithGatewaysFunction = new ResolveWithGatewaysFunction();
            resolveWithGatewaysFunction.Name = name;
            resolveWithGatewaysFunction.Data = data;
            resolveWithGatewaysFunction.Gateways = gateways;

            return ContractHandler.QueryDeserializingToObjectAsync<ResolveWithGatewaysFunction, ResolveWithGatewaysOutputDTO>(resolveWithGatewaysFunction, blockParameter);
        }

        public Task<ReverseOutputDTO> ReverseQueryAsync(ReverseFunction reverseFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ReverseFunction, ReverseOutputDTO>(reverseFunction, blockParameter);
        }

        public Task<ReverseOutputDTO> ReverseQueryAsync(byte[] lookupAddress, BigInteger coinType, BlockParameter blockParameter = null)
        {
            var reverseFunction = new ReverseFunction();
            reverseFunction.LookupAddress = lookupAddress;
            reverseFunction.CoinType = coinType;

            return ContractHandler.QueryDeserializingToObjectAsync<ReverseFunction, ReverseOutputDTO>(reverseFunction, blockParameter);
        }

        public Task<ReverseWithGatewaysOutputDTO> ReverseWithGatewaysQueryAsync(ReverseWithGatewaysFunction reverseWithGatewaysFunction, BlockParameter blockParameter = null)
        {
            return ContractHandler.QueryDeserializingToObjectAsync<ReverseWithGatewaysFunction, ReverseWithGatewaysOutputDTO>(reverseWithGatewaysFunction, blockParameter);
        }

        public Task<ReverseWithGatewaysOutputDTO> ReverseWithGatewaysQueryAsync(byte[] lookupAddress, BigInteger coinType, List<string> gateways, BlockParameter blockParameter = null)
        {
            var reverseWithGatewaysFunction = new ReverseWithGatewaysFunction();
            reverseWithGatewaysFunction.LookupAddress = lookupAddress;
            reverseWithGatewaysFunction.CoinType = coinType;
            reverseWithGatewaysFunction.Gateways = gateways;

            return ContractHandler.QueryDeserializingToObjectAsync<ReverseWithGatewaysFunction, ReverseWithGatewaysOutputDTO>(reverseWithGatewaysFunction, blockParameter);
        }

        public Task<FindResolverOutputDTO> FindResolverQueryAsync(byte[] name, BlockParameter blockParameter = null)
        {
            var findResolverFunction = new FindResolverFunction();
            findResolverFunction.Name = name;

            return ContractHandler.QueryDeserializingToObjectAsync<FindResolverFunction, FindResolverOutputDTO>(findResolverFunction, blockParameter);
        }

        public Task<bool> SupportsInterfaceQueryAsync(byte[] interfaceId, BlockParameter blockParameter = null)
        {
            var supportsInterfaceFunction = new SupportsInterfaceFunction();
            supportsInterfaceFunction.InterfaceId = interfaceId;

            return ContractHandler.QueryAsync<SupportsInterfaceFunction, bool>(supportsInterfaceFunction, blockParameter);
        }
#endif

        public override List<Type> GetAllFunctionTypes()
        {
            return new List<Type>
            {
                typeof(BatchGatewayProviderFunction),
                typeof(CcipBatchFunction),
                typeof(CcipBatchCallbackFunction),
                typeof(CcipReadCallbackFunction),
                typeof(FindResolverFunction),
                typeof(RegistryFunction),
                typeof(RequireResolverFunction),
                typeof(ResolveFunction),
                typeof(ResolveBatchCallbackFunction),
                typeof(ResolveCallbackFunction),
                typeof(ResolveDirectCallbackFunction),
                typeof(ResolveDirectCallbackErrorFunction),
                typeof(ResolveWithGatewaysFunction),
                typeof(ResolveWithResolverFunction),
                typeof(ReverseFunction),
                typeof(ReverseAddressCallbackFunction),
                typeof(ReverseNameCallbackFunction),
                typeof(ReverseWithGatewaysFunction),
                typeof(SupportsInterfaceFunction)
            };
        }

        public override List<Type> GetAllEventTypes()
        {
            return new List<Type>();
        }

        public override List<Type> GetAllErrorTypes()
        {
            return new List<Type>
            {
                typeof(DNSDecodingFailedError),
                typeof(DNSEncodingFailedError),
                typeof(EmptyAddressError),
                typeof(HttpErrorError),
                typeof(InvalidBatchGatewayResponseError),
                typeof(OffchainLookupError),
                typeof(OffsetOutOfBoundsErrorError),
                typeof(ResolverErrorError),
                typeof(ResolverNotContractError),
                typeof(ResolverNotFoundError),
                typeof(ReverseAddressMismatchError),
                typeof(UnsupportedResolverProfileError)
            };
        }
    }
}
