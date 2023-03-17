using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
#if !DOTNET35
using System.Net.Http;
#endif
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Newtonsoft.Json;

namespace Nethereum.Contracts.Standards.ENS
{

    public class ENSService
    {
        private readonly IEthApiContractService _ethApiContractService;
        public static string REVERSE_NAME_SUFFIX = ".addr.reverse";
        public const string ENS_ZERO_ADDRESS = "0x0000000000000000000000000000000000000000";
        public static int MaxLookupRedirects { get; set; } = 10; //at least 4

        public ENSService(IEthApiContractService ethApiContractService, string ensRegistryAddress = CommonAddresses.ENS_REGISTRY_ADDRESS, IEnsCCIPService ccipService = null)
        {
            if (ethApiContractService == null) throw new ArgumentNullException(nameof(ethApiContractService));
            _ethApiContractService = ethApiContractService;
            EnsRegistryAddress = ensRegistryAddress ?? throw new ArgumentNullException(nameof(ensRegistryAddress));
#if !DOTNET35
            if (ccipService == null) ccipService = new EnsCCIPService();
#endif
            CCIPService = ccipService;
            _ensUtil = new EnsUtil();
            ENSRegistryService = new ENSRegistryService(ethApiContractService, EnsRegistryAddress);
        }

        public string EnsRegistryAddress { get; }
        public IEnsCCIPService CCIPService { get; }
        public ENSRegistryService ENSRegistryService { get; private set; }
        
        private readonly EnsUtil _ensUtil;
#if !DOTNET35
        public async Task<string> ResolveAddressAsync(string fullName)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName);
            var addrFunction = new AddrFunction();
            addrFunction.Node = fullNameNode.HexToByteArray();
            var result = await ResolveAsync<AddrFunction, AddrOutputDTO>(addrFunction, fullName).ConfigureAwait(false);
            return result.ReturnValue1;
        }


        public async Task<TFunctionOutputDTO> ResolveAsync<TFunction, TFunctionOutputDTO>(TFunction function, string fullName) where TFunction:FunctionMessage, new()
                                                                                                                                where TFunctionOutputDTO : IFunctionOutputDTO, new()
        {
            var resolverAddress = await GetResolverAddressAsync(fullName);
            var resolverService = new PublicResolverService(_ethApiContractService, resolverAddress);
            var supportsENSIP10 = await resolverService.SupportsInterfaceQueryAsync(OffchainResolverService.ENSIP_10_INTERFACEID.HexToByteArray()).ConfigureAwait(false);
            if (supportsENSIP10)
            {
                var callData = function.GetCallData();
                var result = await GetOfflineDataAsync(_ethApiContractService, resolverAddress, fullName, callData).ConfigureAwait(false);
                return new TFunctionOutputDTO().DecodeOutput(result.ToHex());
            }
            else
            {
                var contractHandler = _ethApiContractService.GetContractHandler(resolverAddress);
                return await contractHandler.QueryAsync<TFunction, TFunctionOutputDTO>(function).ConfigureAwait(false);
            }
        }

        public async Task<ABIOutputDTO> ResolveABIAsync(string fullName, AbiTypeContentType abiTypeContentType)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName);
            var aBIFunction = new ABIFunction();
            aBIFunction.Node = fullNameNode.HexToByteArray();
            aBIFunction.ContentTypes = (int)abiTypeContentType;
            return await ResolveAsync<ABIFunction, ABIOutputDTO>(aBIFunction, fullName).ConfigureAwait(false);
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
            var fullNameNode = _ensUtil.GetNameHash(fullName);
            var textFunction = new TextFunction();
            textFunction.Node = fullNameNode.HexToByteArray();
            textFunction.Key = textDataKey.GetDataKeyAsString();
            var result =  await ResolveAsync<TextFunction, TextOutputDTO>(textFunction, fullName).ConfigureAwait(false);
            return result.ReturnValue1;
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

        public async Task<TransactionReceipt> SetTextRequestAndWaitForReceiptAsync(string fullName, TextDataKey textDataKey, string value, CancellationToken cancellationToken = default)
        {
            var fullNameNode = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var resolverService = await GetResolverAsync(fullNameNode).ConfigureAwait(false);
            return await resolverService.SetTextRequestAndWaitForReceiptAsync(fullNameNode, textDataKey.GetDataKeyAsString(), value, cancellationToken).ConfigureAwait(false);
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

        private async Task<byte[]> GetOfflineDataAsync(IEthApiContractService ethApiContractService, string resolverAddress, string fullName, byte[] callData)
        {
            var offchainResolver = new OffchainResolverService(ethApiContractService, resolverAddress);
            var dnsEncoded = _ensUtil.DnsEncode(fullName);
            try
            {
                return await offchainResolver.ResolveQueryAsync(dnsEncoded.HexToByteArray(), callData);
            }
            catch (SmartContractCustomErrorRevertException customError)
            {
                if (customError.IsCustomErrorFor<OffchainLookupError>())
                {

                    var decoded = customError.DecodeError<OffchainLookupError>();
                    if (!decoded.Sender.IsTheSameAddress(resolverAddress))
                    {
                        throw new Exception("Cannot handle OffchainLookup raised inside nested call");
                    }
                    return await CCIPService.ResolveCCIPRead(offchainResolver, decoded, MaxLookupRedirects);

                }
                else
                {
                    throw customError;
                }
            }

        }

        public async Task<string> GetResolverAddressAsync(string name)
        {
            if (name == null) return null;
            if (name == "." || !name.Contains(".")) return null;
            
            var nameHash = _ensUtil.GetNameHash(name).HexToByteArray();
            var resolverAddress = await ENSRegistryService.ResolverQueryAsync(nameHash);

            if (resolverAddress.IsTheSameAddress(ENS_ZERO_ADDRESS))
            {
                return await GetResolverAddressAsync(_ensUtil.GetParent(name));
            }
            else
            {
                return resolverAddress;
            }
        }
#endif
    }

}
