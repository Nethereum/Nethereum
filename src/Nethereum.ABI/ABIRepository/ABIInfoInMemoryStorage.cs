using Nethereum.ABI.Model;
using Nethereum.Hex.HexConvertors.Extensions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

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
            return _abiInfos.FirstOrDefault(x => x.Address == contractAddress && chainId == x.ChainId);
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo == null) return null;
            return abiInfo.ContractABI.FindFunctionABI(signature);
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            var abiInfo = GetABIInfo(chainId, contractAddress);
            if (abiInfo == null) return null;
            return abiInfo.ContractABI.FindFunctionABIFromInputData(inputData);
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
            if (abiInfo == null) return null;
            return abiInfo.ContractABI.FindEventABI(signature);
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            return _signatureToFunctionABIDictionary[signature];
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            return _signatureToErrorABIDictionary[signature];
        }

        public List<EventABI> FindEventABI(string signature)
        {
            return _signatureToEventABIDictionary[signature];
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
            if (!ModelExtensions.ValiInputDataSignature(inputData)) return null;
            var signature = ModelExtensions.GetSignatureFromData(inputData);
            return FindFunctionABI(signature);
        }

        public void AddEventABI(EventABI eventABI)
        {
            if (_signatureToEventABIDictionary.ContainsKey(eventABI.Sha3Signature))
            {
                var eventABIs = _signatureToEventABIDictionary[eventABI.Sha3Signature];
                if (!eventABIs.Any(x => x.HasTheSameSignatureValues(eventABI)))
                {
                    eventABIs.Add(eventABI);
                }
            }
            else
            {
                this._signatureToEventABIDictionary.Add(eventABI.Sha3Signature, new List<EventABI> { eventABI });
            }
        }

        public void AddErrorABI(ErrorABI errorABI)
        {
            if (_signatureToErrorABIDictionary.ContainsKey(errorABI.Sha3Signature))
            {
                var errorABIs = _signatureToErrorABIDictionary[errorABI.Sha3Signature];
                if (!errorABIs.Any(x => x.HasTheSameSignatureValues(errorABI)))
                {
                    errorABIs.Add(errorABI);
                }
            }
            else
            {
                this._signatureToErrorABIDictionary.Add(errorABI.Sha3Signature, new List<ErrorABI> { errorABI });
            }
        }

        public void AddFunctionABI(FunctionABI functionABI)
        {
            if (_signatureToFunctionABIDictionary.ContainsKey(functionABI.Sha3Signature))
            {
                var functionABIs = _signatureToFunctionABIDictionary[functionABI.Sha3Signature];
                if (!functionABIs.Any(x => x.HasTheSameSignatureValues(functionABI)))
                {
                    functionABIs.Add(functionABI);
                }
            }
            else
            {
                this._signatureToFunctionABIDictionary.Add(functionABI.Sha3Signature, new List<FunctionABI> { functionABI });
            }
        }
    }
}
