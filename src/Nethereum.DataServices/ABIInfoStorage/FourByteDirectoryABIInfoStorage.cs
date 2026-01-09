using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using Nethereum.DataServices.FourByteDirectory;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class FourByteDirectoryABIInfoStorage : IABIInfoStorage
    {
        private readonly FourByteDirectoryService _directoryService;
        private readonly ABIStringSignatureDeserialiser _signatureDeserialiser = new ABIStringSignatureDeserialiser();

        public FourByteDirectoryABIInfoStorage() : this(new FourByteDirectoryService())
        {
        }

        public FourByteDirectoryABIInfoStorage(FourByteDirectoryService directoryService)
        {
            _directoryService = directoryService;
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            return null;
        }

        public Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)
        {
            return Task.FromResult<ABIInfo>(null);
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            return LookupFunctionAsync(signature).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            if (string.IsNullOrEmpty(inputData) || inputData.Length < 10)
                return null;

            var selector = inputData.StartsWith("0x")
                ? inputData.Substring(0, 10)
                : "0x" + inputData.Substring(0, 8);

            return LookupFunctionAsync(selector).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            return LookupEventAsync(signature).ConfigureAwait(false).GetAwaiter().GetResult();
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            return null;
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            var result = LookupFunctionAsync(signature).ConfigureAwait(false).GetAwaiter().GetResult();
            return result != null ? new List<FunctionABI> { result } : new List<FunctionABI>();
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            if (string.IsNullOrEmpty(inputData) || inputData.Length < 10)
                return new List<FunctionABI>();

            var selector = inputData.StartsWith("0x")
                ? inputData.Substring(0, 10)
                : "0x" + inputData.Substring(0, 8);

            var result = LookupFunctionAsync(selector).ConfigureAwait(false).GetAwaiter().GetResult();
            return result != null ? new List<FunctionABI> { result } : new List<FunctionABI>();
        }

        public List<EventABI> FindEventABI(string signature)
        {
            var result = LookupEventAsync(signature).ConfigureAwait(false).GetAwaiter().GetResult();
            return result != null ? new List<EventABI> { result } : new List<EventABI>();
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            return new List<ErrorABI>();
        }

        public async Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            return await LookupFunctionAsync(signature).ConfigureAwait(false);
        }

        public async Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData)
        {
            if (string.IsNullOrEmpty(inputData) || inputData.Length < 10)
                return null;

            var selector = inputData.StartsWith("0x")
                ? inputData.Substring(0, 10)
                : "0x" + inputData.Substring(0, 8);

            return await LookupFunctionAsync(selector).ConfigureAwait(false);
        }

        public async Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            return await LookupEventAsync(signature).ConfigureAwait(false);
        }

        public Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            return Task.FromResult<ErrorABI>(null);
        }

        public async Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, FunctionABI>();
            var signatureList = signatures?.ToList();
            if (signatureList == null || signatureList.Count == 0)
                return result;

            foreach (var sig in signatureList)
            {
                try
                {
                    var parsed = await LookupFunctionAsync(sig).ConfigureAwait(false);
                    if (parsed != null)
                        result[sig] = parsed;
                }
                catch { }
            }

            return result;
        }

        public async Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, EventABI>();
            var signatureList = signatures?.ToList();
            if (signatureList == null || signatureList.Count == 0)
                return result;

            foreach (var sig in signatureList)
            {
                try
                {
                    var parsed = await LookupEventAsync(sig).ConfigureAwait(false);
                    if (parsed != null)
                        result[sig] = parsed;
                }
                catch { }
            }

            return result;
        }

        public async Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)
        {
            var result = new ABIBatchResult();
            var funcList = functionSignatures?.ToList() ?? new List<string>();
            var eventList = eventSignatures?.ToList() ?? new List<string>();

            foreach (var sig in funcList)
            {
                try
                {
                    var parsed = await LookupFunctionAsync(sig).ConfigureAwait(false);
                    if (parsed != null)
                        result.Functions[sig] = parsed;
                }
                catch { }
            }

            foreach (var sig in eventList)
            {
                try
                {
                    var parsed = await LookupEventAsync(sig).ConfigureAwait(false);
                    if (parsed != null)
                        result.Events[sig] = parsed;
                }
                catch { }
            }

            return result;
        }

        private async Task<FunctionABI> LookupFunctionAsync(string selector)
        {
            if (string.IsNullOrEmpty(selector)) return null;
            try
            {
                var response = await _directoryService.GetFunctionSignatureByHexSignatureAsync(selector).ConfigureAwait(false);
                if (response?.Signatures != null && response.Signatures.Count > 0)
                {
                    return ParseFunctionSignature(response.Signatures[0].TextSignature);
                }
            }
            catch { }
            return null;
        }

        private async Task<EventABI> LookupEventAsync(string signature)
        {
            if (string.IsNullOrEmpty(signature)) return null;
            try
            {
                var response = await _directoryService.GetEventSignatureByHexSignatureAsync(signature).ConfigureAwait(false);
                if (response?.Signatures != null && response.Signatures.Count > 0)
                {
                    return ParseEventSignature(response.Signatures[0].TextSignature);
                }
            }
            catch { }
            return null;
        }

        private FunctionABI ParseFunctionSignature(string signature)
        {
            if (string.IsNullOrEmpty(signature)) return null;
            var match = Regex.Match(signature, @"^(\w+)\((.*)?\)$");
            if (!match.Success) return null;
            var name = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            return _signatureDeserialiser.ExtractFunctionABI(signature, name, parameters);
        }

        private EventABI ParseEventSignature(string signature)
        {
            if (string.IsNullOrEmpty(signature)) return null;
            var match = Regex.Match(signature, @"^(\w+)\((.*)?\)$");
            if (!match.Success) return null;
            var name = match.Groups[1].Value;
            var parameters = match.Groups[2].Value;
            return _signatureDeserialiser.ExtractEventABI(signature, name, parameters);
        }
    }
}
