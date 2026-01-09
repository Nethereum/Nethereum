using Nethereum.ABI.ABIDeserialisation;
using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using Nethereum.DataServices.Sourcify;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class Sourcify4ByteABIInfoStorage : IABIInfoStorage
    {
        private readonly Sourcify4ByteSignatureService _signatureService;
        private readonly ABIStringSignatureDeserialiser _signatureDeserialiser = new ABIStringSignatureDeserialiser();

        public Sourcify4ByteABIInfoStorage() : this(new Sourcify4ByteSignatureService())
        {
        }

        public Sourcify4ByteABIInfoStorage(Sourcify4ByteSignatureService signatureService)
        {
            _signatureService = signatureService;
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

            try
            {
                var response = await _signatureService.LookupAsync(signatureList, null).ConfigureAwait(false);
                if (response?.Ok == true && response.Result?.Function != null)
                {
                    foreach (var sig in signatureList)
                    {
                        if (response.Result.Function.TryGetValue(sig, out var matches) && matches.Count > 0)
                        {
                            var parsed = ParseFunctionSignature(matches[0].Name);
                            if (parsed != null)
                                result[sig] = parsed;
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        public async Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, EventABI>();
            var signatureList = signatures?.ToList();
            if (signatureList == null || signatureList.Count == 0)
                return result;

            try
            {
                var response = await _signatureService.LookupAsync(null, signatureList).ConfigureAwait(false);
                if (response?.Ok == true && response.Result?.Event != null)
                {
                    foreach (var sig in signatureList)
                    {
                        if (response.Result.Event.TryGetValue(sig, out var matches) && matches.Count > 0)
                        {
                            var parsed = ParseEventSignature(matches[0].Name);
                            if (parsed != null)
                                result[sig] = parsed;
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        public async Task<ABIBatchResult> FindABIsBatchAsync(IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)
        {
            var result = new ABIBatchResult();
            var funcList = functionSignatures?.ToList() ?? new List<string>();
            var eventList = eventSignatures?.ToList() ?? new List<string>();

            if (funcList.Count == 0 && eventList.Count == 0)
                return result;

            try
            {
                var response = await _signatureService.LookupAsync(
                    funcList.Count > 0 ? funcList : null,
                    eventList.Count > 0 ? eventList : null).ConfigureAwait(false);

                if (response?.Ok == true && response.Result != null)
                {
                    if (response.Result.Function != null)
                    {
                        foreach (var sig in funcList)
                        {
                            if (response.Result.Function.TryGetValue(sig, out var matches) && matches.Count > 0)
                            {
                                var parsed = ParseFunctionSignature(matches[0].Name);
                                if (parsed != null)
                                    result.Functions[sig] = parsed;
                            }
                        }
                    }

                    if (response.Result.Event != null)
                    {
                        foreach (var sig in eventList)
                        {
                            if (response.Result.Event.TryGetValue(sig, out var matches) && matches.Count > 0)
                            {
                                var parsed = ParseEventSignature(matches[0].Name);
                                if (parsed != null)
                                    result.Events[sig] = parsed;
                            }
                        }
                    }
                }
            }
            catch { }

            return result;
        }

        private async Task<FunctionABI> LookupFunctionAsync(string selector)
        {
            if (string.IsNullOrEmpty(selector)) return null;
            try
            {
                var response = await _signatureService.LookupFunctionAsync(selector).ConfigureAwait(false);
                if (response?.Ok == true && response.Result?.Function != null)
                {
                    if (response.Result.Function.TryGetValue(selector, out var signatures) && signatures.Count > 0)
                    {
                        return ParseFunctionSignature(signatures[0].Name);
                    }
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
                var response = await _signatureService.LookupEventAsync(signature).ConfigureAwait(false);
                if (response?.Ok == true && response.Result?.Event != null)
                {
                    if (response.Result.Event.TryGetValue(signature, out var signatures) && signatures.Count > 0)
                    {
                        return ParseEventSignature(signatures[0].Name);
                    }
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
