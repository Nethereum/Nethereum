using System;
using System.Threading.Tasks;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Contracts.Standards.ENS
{
    public class ENSService
    {
        private readonly IEthApiContractService _ethApiContractService;
        public static string REVERSE_NAME_SUFFIX = ".addr.reverse";

        public ENSService(IEthApiContractService ethApiContractService, string ensRegistryAddress = "0x00000000000C2E074eC69A0dFb2997BA6C7d2e1e")
        {
            if (ethApiContractService == null) throw new ArgumentNullException(nameof(ethApiContractService));
            _ethApiContractService = ethApiContractService;
            EnsRegistryAddress = ensRegistryAddress ?? throw new ArgumentNullException(nameof(ensRegistryAddress));
            _ensUtil = new EnsUtil();
            ENSRegistryService = new ENSRegistryService(ethApiContractService, EnsRegistryAddress);
        }

        public string EnsRegistryAddress { get; }
        public ENSRegistryService ENSRegistryService { get; private set; }
        
        private readonly EnsUtil _ensUtil;
#if !DOTNET35
        public async Task<string> ResolveAddressAsync(string fullName)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.AddrQueryAsync(fullNameNode).ConfigureAwait(false);
        }

        public async Task<ABIOutputDTO> ResolveABIAsync(string fullName, AbiTypeContentType abiTypeContentType)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.ABIQueryAsync(fullNameNode, (int)abiTypeContentType).ConfigureAwait(false);
        }

        public Task<string> SetSubnodeOwnerRequestAsync(string fullName, string label, string owner)
        {
            var fullNameHash = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var labelHash = _ensUtil.GetLabelHash(label).HexToByteArray();
            return ENSRegistryService.SetSubnodeOwnerRequestAsync(fullNameHash, labelHash, owner);
        }

        public Task<TransactionReceipt> SetSubnodeOwnerRequestAndWaitForReceiptAsync(string fullName, string label, string owner)
        {
            var fullNameHash = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var labelHash = _ensUtil.GetLabelHash(label).HexToByteArray();
            return ENSRegistryService.SetSubnodeOwnerRequestAndWaitForReceiptAsync(fullNameHash, labelHash, owner);
        }

        public async Task<string> ResolveTextAsync(string fullName, TextDataKey textDataKey)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.TextQueryAsync(fullNameNode, textDataKey.GetDataKeyAsString()).ConfigureAwait(false);
        }

        public async Task<string> SetTextRequestAsync(string fullName, TextDataKey textDataKey, string value)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetTextRequestAsync(fullNameNode, textDataKey.GetDataKeyAsString(), value).ConfigureAwait(false);
        }

        public async Task<string> SetAddressRequestAsync(string fullName, string address)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetAddrRequestAsync(fullNameNode, address).ConfigureAwait(false);
        }

        public async Task<string> SetContentHashRequestAsync(string fullName, string contentHashInHex)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetContenthashRequestAsync(fullNameNode, contentHashInHex.HexToByteArray()).ConfigureAwait(false);
        }

        public async Task<byte[]> GetContentHashAsync(string fullName)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.ContenthashQueryAsync(fullNameNode).ConfigureAwait(false);
        }

        public async Task<TransactionReceipt> SetTextRequestAndWaitForReceiptAsync(string fullName, TextDataKey textDataKey, string value)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetTextRequestAndWaitForReceiptAsync(fullNameNode, textDataKey.GetDataKeyAsString(), value).ConfigureAwait(false);
        }

        public Task<PublicResolverService> GetResolverAsync(string fullNameNode)
        {
            var fullNameNodeAsBytes = new EnsUtil().GetNameHash(fullNameNode).HexToByteArray();
            return GetResolverAsync(fullNameNodeAsBytes);
        }

        public async Task<string> ReverseResolveAsync(string address)
        {
           var addressReverse = address.RemoveHexPrefix().ToLower() + REVERSE_NAME_SUFFIX;
           var fullNameNode = _ensUtil.GetNameHash(addressReverse).HexToByteArray();
           var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
           return await resolverService.NameQueryAsync(fullNameNode).ConfigureAwait(false);
        }

        public async Task<PublicResolverService> GetResolverAsync(byte[] fullNameNode)
        {
            var resolverAddress = await ENSRegistryService.ResolverQueryAsync(fullNameNode).ConfigureAwait(false);
            var resolverService = new PublicResolverService(_ethApiContractService, resolverAddress);
            return resolverService;

        }
#endif
    }

}
