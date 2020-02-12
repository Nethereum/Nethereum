using System;
using System.Threading.Tasks;
using Nethereum.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;

namespace Nethereum.ENS
{
    public class ENSService
    {
        public static string REVERSE_NAME_SUFFIX = ".addr.reverse";

        public ENSService(Web3.Web3 web3, string ensRegistryAddress = "0x00000000000C2E074eC69A0dFb2997BA6C7d2e1e")
        {
            if (string.IsNullOrEmpty(ensRegistryAddress))
                throw new ArgumentException("ensRegistryAddress cannot be null", nameof(ensRegistryAddress));
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            EnsRegistryAddress = ensRegistryAddress;
            _ensUtil = new EnsUtil();
            ENSRegistryService = new ENSRegistryService(Web3, EnsRegistryAddress);
        }

        public Web3.Web3 Web3 { get; }
        public string EnsRegistryAddress { get; }
        public ENSRegistryService ENSRegistryService { get; private set; }
        
        private readonly EnsUtil _ensUtil;

        public async Task<string> ResolveAddressAsync(string fullName)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.AddrQueryAsync(fullNameNode);
        }

        public async Task<ABIOutputDTO> ResolveABIAsync(string fullName, AbiTypeContentType abiTypeContentType)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.ABIQueryAsync(fullNameNode, (int)abiTypeContentType);
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
            return await resolverService.TextQueryAsync(fullNameNode, textDataKey.GetDataKeyAsString());
        }

        public async Task<string> SetTextRequestAsync(string fullName, TextDataKey textDataKey, string value)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetTextRequestAsync(fullNameNode, textDataKey.GetDataKeyAsString(), value);
        }

        public async Task<string> SetAddressRequestAsync(string fullName, string address)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetAddrRequestAsync(fullNameNode, address);
        }

        public async Task<string> SetContentHashRequestAsync(string fullName, string contentHashInHex)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetContenthashRequestAsync(fullNameNode, contentHashInHex.HexToByteArray());
        }

        public async Task<byte[]> GetContentHashAsync(string fullName)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.ContenthashQueryAsync(fullNameNode);
        }

        public async Task<TransactionReceipt> SetTextRequestAndWaitForReceipt(string fullName, TextDataKey textDataKey, string value)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetTextRequestAndWaitForReceiptAsync(fullNameNode, textDataKey.GetDataKeyAsString(), value);
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
           return await resolverService.NameQueryAsync(fullNameNode);
        }

        public async Task<PublicResolverService> GetResolverAsync(byte[] fullNameNode)
        {
            var resolverAddress = await ENSRegistryService.ResolverQueryAsync(fullNameNode).ConfigureAwait(false);
            var resolverService = new PublicResolverService(Web3, resolverAddress);
            return resolverService;
        }
    }
}
