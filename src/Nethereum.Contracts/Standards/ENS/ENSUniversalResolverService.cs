using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts.Constants;
using Nethereum.Contracts.Services;
using Nethereum.Contracts.Standards.ENS.Multicallable.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.PublicResolver.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.UniversalResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.Contracts.Standards.ENS
{
#if !DOTNET35
    public class ENSUniversalResolverService
    {
        public const long ETH_COIN_TYPE = 60;

        private readonly IEthApiContractService _ethApiContractService;
        private readonly EnsUtil _ensUtil = new EnsUtil();
        private readonly IEnsCCIPService _ccipService;

        public string UniversalResolverAddress { get; }
        public UniversalResolver.UniversalResolverService UniversalResolverService { get; }

        /// <summary>Maximum EIP-3668 CCIP-Read redirects to follow (at least 4 per the spec).</summary>
        public int MaxLookupRedirects { get; set; } = 10;

        public ENSUniversalResolverService(IEthApiContractService ethApiContractService,
            string universalResolverAddress = CommonAddresses.UNIVERSAL_RESOLVER_ADDRESS,
            IEnsCCIPService ccipService = null)
        {
            _ethApiContractService = ethApiContractService;
            UniversalResolverAddress = universalResolverAddress;
            UniversalResolverService = new UniversalResolver.UniversalResolverService(ethApiContractService, universalResolverAddress);
            _ccipService = ccipService ?? new EnsCCIPService();
        }

        public async Task<string> ResolveAddressAsync(string fullName)
        {
            var node = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var output = await ResolveProfileAsync<AddrFunction, AddrOutputDTO>(fullName, new AddrFunction { Node = node }).ConfigureAwait(false);
            return output?.ReturnValue1;
        }

        public Task<string> ResolveTextAsync(string fullName, TextDataKey textDataKey)
        {
            return ResolveTextAsync(fullName, textDataKey.GetDataKeyAsString());
        }

        public async Task<string> ResolveTextAsync(string fullName, string key)
        {
            var node = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var output = await ResolveProfileAsync<TextFunction, TextOutputDTO>(fullName, new TextFunction { Node = node, Key = key }).ConfigureAwait(false);
            return output?.ReturnValue1;
        }

        public async Task<byte[]> GetContentHashAsync(string fullName)
        {
            var node = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var output = await ResolveProfileAsync<ContenthashFunction, ContenthashOutputDTO>(fullName, new ContenthashFunction { Node = node }).ConfigureAwait(false);
            return output?.ReturnValue1;
        }

        public Task<ABIOutputDTO> ResolveABIAsync(string fullName, AbiTypeContentType abiTypeContentType)
        {
            var node = _ensUtil.GetNameHash(fullName).HexToByteArray();
            return ResolveProfileAsync<ABIFunction, ABIOutputDTO>(fullName, new ABIFunction { Node = node, ContentTypes = (int)abiTypeContentType });
        }

        public async Task<string> ReverseResolveAsync(string address, BigInteger coinType = default)
        {
            if (coinType == default) coinType = ETH_COIN_TYPE;
            ReverseOutputDTO result;
            try
            {
                result = await UniversalResolverService
                    .ReverseQueryAsync(address.HexToByteArray(), coinType).ConfigureAwait(false);
            }
            catch (SmartContractCustomErrorRevertException customError)
            {
                var resultBytes = await HandleOffchainLookupAsync(customError).ConfigureAwait(false);
                result = new ReverseOutputDTO().DecodeOutput(resultBytes.ToHex());
            }
            return string.IsNullOrEmpty(result.ReturnValue1) ? null : result.ReturnValue1;
        }

        public Task<EnsRecord> ResolveRecordAsync(string fullName, params TextDataKey[] textKeys)
        {
            return ResolveRecordAsync(fullName, textKeys.Select(k => k.GetDataKeyAsString()).ToArray());
        }

        public async Task<EnsRecord> ResolveRecordAsync(string fullName, IEnumerable<string> textKeys)
        {
            var keys = textKeys?.ToArray() ?? new string[0];
            var node = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var dnsEncodedName = _ensUtil.DnsEncode(fullName).HexToByteArray();
            var data = BuildRecordMulticallData(node, keys);

            var result = await ResolveDataAsync(dnsEncodedName, data).ConfigureAwait(false);
            var innerResults = new MulticallOutputDTO().DecodeOutput(result.ReturnValue1.ToHex()).ReturnValue1;
            return DecodeRecord(fullName, innerResults, keys);
        }

        public async Task<Dictionary<string, string>> ResolveTextsAsync(string fullName, params string[] keys)
        {
            var node = _ensUtil.GetNameHash(fullName).HexToByteArray();
            var dnsEncodedName = _ensUtil.DnsEncode(fullName).HexToByteArray();
            var calls = keys.Select(k => new TextFunction { Node = node, Key = k }.GetCallData()).ToList();
            var data = new MulticallFunction { Data = calls }.GetCallData();

            var result = await ResolveDataAsync(dnsEncodedName, data).ConfigureAwait(false);
            var innerResults = new MulticallOutputDTO().DecodeOutput(result.ReturnValue1.ToHex()).ReturnValue1;

            var texts = new Dictionary<string, string>();
            for (var i = 0; i < keys.Length; i++)
            {
                texts[keys[i]] = DecodeProfile<TextOutputDTO>(innerResults, i)?.ReturnValue1;
            }
            return texts;
        }

        public Task<List<EnsRecord>> ResolveRecordsAsync(IEnumerable<string> fullNames, params TextDataKey[] textKeys)
        {
            return ResolveRecordsAsync(fullNames, textKeys.Select(k => k.GetDataKeyAsString()).ToArray());
        }

        public async Task<List<EnsRecord>> ResolveRecordsAsync(IEnumerable<string> fullNames, IEnumerable<string> textKeys)
        {
            var keys = textKeys?.ToArray() ?? new string[0];
            var tasks = fullNames.Select(name => ResolveRecordOrEmptyAsync(name, keys)).ToList();
            var records = await Task.WhenAll(tasks).ConfigureAwait(false);
            return records.ToList();
        }

        private async Task<EnsRecord> ResolveRecordOrEmptyAsync(string fullName, string[] textKeys)
        {
            try
            {
                return await ResolveRecordAsync(fullName, textKeys).ConfigureAwait(false);
            }
            catch (SmartContractCustomErrorRevertException)
            {
                return new EnsRecord { Name = fullName };
            }
        }

        private async Task<TFunctionOutputDTO> ResolveProfileAsync<TFunction, TFunctionOutputDTO>(string fullName, TFunction function)
            where TFunction : FunctionMessage, new()
            where TFunctionOutputDTO : IFunctionOutputDTO, new()
        {
            var dnsEncodedName = _ensUtil.DnsEncode(fullName).HexToByteArray();
            var result = await ResolveDataAsync(dnsEncodedName, function.GetCallData()).ConfigureAwait(false);
            if (result.ReturnValue1 == null || result.ReturnValue1.Length == 0) return default;
            return new TFunctionOutputDTO().DecodeOutput(result.ReturnValue1.ToHex());
        }

        /// <summary>
        /// Calls the Universal Resolver resolve(name, data); on an EIP-3668 OffchainLookup it routes the
        /// gateway exchange through the CCIP service and decodes the final resolve(bytes,address) result.
        /// </summary>
        private async Task<ResolveOutputDTO> ResolveDataAsync(byte[] dnsEncodedName, byte[] data)
        {
            try
            {
                return await UniversalResolverService.ResolveQueryAsync(dnsEncodedName, data).ConfigureAwait(false);
            }
            catch (SmartContractCustomErrorRevertException customError)
            {
                var resultBytes = await HandleOffchainLookupAsync(customError).ConfigureAwait(false);
                return new ResolveOutputDTO().DecodeOutput(resultBytes.ToHex());
            }
        }

        private Task<byte[]> HandleOffchainLookupAsync(SmartContractCustomErrorRevertException customError)
        {
            if (!customError.IsCustomErrorFor<OffchainResolver.ContractDefinition.OffchainLookupError>()) throw customError;
            var offchainLookup = customError.DecodeError<OffchainResolver.ContractDefinition.OffchainLookupError>();
            return _ccipService.ResolveCCIPReadGenericAsync(_ethApiContractService, offchainLookup, MaxLookupRedirects);
        }

        private byte[] BuildRecordMulticallData(byte[] node, string[] textKeys)
        {
            var calls = new List<byte[]>
            {
                new AddrFunction { Node = node }.GetCallData(),
                new ContenthashFunction { Node = node }.GetCallData()
            };
            calls.AddRange(textKeys.Select(k => new TextFunction { Node = node, Key = k }.GetCallData()));
            return new MulticallFunction { Data = calls }.GetCallData();
        }

        private EnsRecord DecodeRecord(string fullName, List<byte[]> innerResults, string[] textKeys)
        {
            var record = new EnsRecord { Name = fullName };
            record.Address = DecodeProfile<AddrOutputDTO>(innerResults, 0)?.ReturnValue1;
            record.ContentHash = DecodeProfile<ContenthashOutputDTO>(innerResults, 1)?.ReturnValue1;
            for (var i = 0; i < textKeys.Length; i++)
            {
                var value = DecodeProfile<TextOutputDTO>(innerResults, 2 + i)?.ReturnValue1;
                if (value != null) record.Texts[textKeys[i]] = value;
            }
            return record;
        }

        private static TFunctionOutputDTO DecodeProfile<TFunctionOutputDTO>(List<byte[]> innerResults, int index)
            where TFunctionOutputDTO : IFunctionOutputDTO, new()
        {
            if (index >= innerResults.Count) return default;
            var element = innerResults[index];
            if (element == null || element.Length == 0 || element.Length % 32 != 0) return default;
            return new TFunctionOutputDTO().DecodeOutput(element.ToHex());
        }
    }
#endif
}
