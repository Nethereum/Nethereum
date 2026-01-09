using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.ABI.ABIRepository
{
    public class ABIInfoInMemoryStorage : IABIInfoStorage
    {
        private List<ABIInfo> _abiInfos = new List<ABIInfo>();
        private IDictionary<string, List<FunctionABI>> _signatureToFunctionABIDictionary { get; set; } = new Dictionary<string, List<FunctionABI>>();
        private IDictionary<string, List<ErrorABI>> _signatureToErrorABIDictionary { get; set; } = new Dictionary<string, List<ErrorABI>>();
        private IDictionary<string, List<EventABI>> _signatureToEventABIDictionary { get; set; } = new Dictionary<string, List<EventABI>>();


        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            contractAddress = contractAddress.ToLowerInvariant();
            return _abiInfos.FirstOrDefault(x => x.Address == contractAddress && x.ChainId.HasValue && (long)chainId == x.ChainId.Value);
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                var result = abiInfo.ContractABI.FindFunctionABI(signature);
                if (result != null) return result;
            }

            var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
            if (_signatureToFunctionABIDictionary.TryGetValue(normalizedSig, out var functions) && functions.Count > 0)
            {
                return functions[0];
            }

            return null;
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                var result = abiInfo.ContractABI.FindFunctionABIFromInputData(inputData);
                if (result != null) return result;
            }

            if (!SignatureEncoder.ValiInputDataSignature(inputData)) return null;
            var signature = SignatureEncoder.GetSignatureFromData(inputData);
            var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
            if (_signatureToFunctionABIDictionary.TryGetValue(normalizedSig, out var functions) && functions.Count > 0)
            {
                return functions[0];
            }

            return null;
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo == null) return null;
            return abiInfo.ContractABI.FindErrorABI(signature);
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo != null)
            {
                var result = abiInfo.ContractABI.FindEventABI(signature);
                if (result != null) return result;
            }

            var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
            if (_signatureToEventABIDictionary.TryGetValue(normalizedSig, out var events) && events.Count > 0)
            {
                return events[0];
            }

            return null;
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            signature = SignatureEncoder.ConvertToStringKey(signature);
            return _signatureToFunctionABIDictionary[signature];
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            signature = SignatureEncoder.ConvertToStringKey(signature);
            return _signatureToErrorABIDictionary[signature];
        }

        public List<EventABI> FindEventABI(string signature)
        {
            signature = SignatureEncoder.ConvertToStringKey(signature);
            return _signatureToEventABIDictionary[signature];
        }

        public void AddABIInfo(string abi) {
            var abiInfo = ABIInfo.FromABI(abi);
            AddABIInfo(abiInfo);
        }

        public void AddABIInfo(string abi, string address, string contractName, string contractType, long? chainId)
        {
            var abiInfo = ABIInfo.FromABI(abi, address, contractName, contractType, chainId);
            AddABIInfo(abiInfo);
        }

        public void AddABIInfo(CompilationMetadata.CompilationMetadata compilationMetadata, string address, string contractName, string contractType, long? chainId)
        {
            var abiInfo = ABIInfo.FromCompilationMetadata(compilationMetadata, address, contractName, contractType, chainId);
            AddABIInfo(abiInfo);
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            _abiInfos.Add(abiInfo);
            abiInfo.InitialiseContractABI();

            foreach (var functionABI in abiInfo.ContractABI.Functions)
            {
                AddFunctionABI(functionABI);

            }

            foreach (var errorABI in abiInfo.ContractABI.Errors)
            {
                AddErrorABI(errorABI);

            }

            foreach (var eventABI in abiInfo.ContractABI.Events)
            {
                AddEventABI(eventABI);
            }
        }
        
        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            if (!SignatureEncoder.ValiInputDataSignature(inputData)) return null;
            var signature = SignatureEncoder.GetSignatureFromData(inputData);
            return FindFunctionABI(signature);
        }

        public void AddEventABI(EventABI eventABI)
        {
            var signature = SignatureEncoder.ConvertToStringKey(eventABI.Sha3Signature);
            if (_signatureToEventABIDictionary.ContainsKey(signature))
            {
                var eventABIs = _signatureToEventABIDictionary[signature];
                if (!eventABIs.Any(x => x.HasTheSameSignatureValues(eventABI)))
                {
                    eventABIs.Add(eventABI);
                }
            }
            else
            {
                this._signatureToEventABIDictionary.Add(signature, new List<EventABI> { eventABI });
            }
        }

        public void AddErrorABI(ErrorABI errorABI)
        {
            var signature = SignatureEncoder.ConvertToStringKey(errorABI.Sha3Signature);
            if (_signatureToErrorABIDictionary.ContainsKey(signature))
            {
                var errorABIs = _signatureToErrorABIDictionary[signature];
                if (!errorABIs.Any(x => x.HasTheSameSignatureValues(errorABI)))
                {
                    errorABIs.Add(errorABI);
                }
            }
            else
            {
                this._signatureToErrorABIDictionary.Add(signature, new List<ErrorABI> { errorABI });
            }
        }

        public void AddFunctionABI(FunctionABI functionABI)
        {
            var signature = SignatureEncoder.ConvertToStringKey(functionABI.Sha3Signature);
            if (_signatureToFunctionABIDictionary.ContainsKey(signature))
            {
                var functionABIs = _signatureToFunctionABIDictionary[signature];
                if (!functionABIs.Any(x => x.HasTheSameSignatureValues(functionABI)))
                {
                    functionABIs.Add(functionABI);
                }
            }
            else
            {
                this._signatureToFunctionABIDictionary.Add(signature, new List<FunctionABI> { functionABI });
            }
        }

        public Task<ABIInfo> GetABIInfoAsync(long chainId, string contractAddress)
        {
            return Task.FromResult(GetABIInfo(new BigInteger(chainId), contractAddress));
        }

        public Task<FunctionABI> FindFunctionABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            return Task.FromResult(FindFunctionABI(chainId, contractAddress, signature));
        }

        public Task<FunctionABI> FindFunctionABIFromInputDataAsync(BigInteger chainId, string contractAddress, string inputData)
        {
            return Task.FromResult(FindFunctionABIFromInputData(chainId, contractAddress, inputData));
        }

        public Task<EventABI> FindEventABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            return Task.FromResult(FindEventABI(chainId, contractAddress, signature));
        }

        public Task<ErrorABI> FindErrorABIAsync(BigInteger chainId, string contractAddress, string signature)
        {
            return Task.FromResult(FindErrorABI(chainId, contractAddress, signature));
        }

        public Task<IDictionary<string, FunctionABI>> FindFunctionABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, FunctionABI>();
            foreach (var signature in signatures)
            {
                var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
                if (_signatureToFunctionABIDictionary.TryGetValue(normalizedSig, out var functions) && functions.Count > 0)
                {
                    result[signature] = functions[0];
                }
            }
            return Task.FromResult<IDictionary<string, FunctionABI>>(result);
        }

        public Task<IDictionary<string, EventABI>> FindEventABIsBatchAsync(IEnumerable<string> signatures)
        {
            var result = new Dictionary<string, EventABI>();
            foreach (var signature in signatures)
            {
                var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
                if (_signatureToEventABIDictionary.TryGetValue(normalizedSig, out var events) && events.Count > 0)
                {
                    result[signature] = events[0];
                }
            }
            return Task.FromResult<IDictionary<string, EventABI>>(result);
        }

        public Task<ABIBatchResult> FindABIsBatchAsync(
            IEnumerable<string> functionSignatures, IEnumerable<string> eventSignatures)
        {
            var result = new ABIBatchResult();

            foreach (var signature in functionSignatures ?? Enumerable.Empty<string>())
            {
                var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
                if (_signatureToFunctionABIDictionary.TryGetValue(normalizedSig, out var funcs) && funcs.Count > 0)
                {
                    result.Functions[signature] = funcs[0];
                }
            }

            foreach (var signature in eventSignatures ?? Enumerable.Empty<string>())
            {
                var normalizedSig = SignatureEncoder.ConvertToStringKey(signature);
                if (_signatureToEventABIDictionary.TryGetValue(normalizedSig, out var evts) && evts.Count > 0)
                {
                    result.Events[signature] = evts[0];
                }
            }

            return Task.FromResult(result);
        }
    }
}
