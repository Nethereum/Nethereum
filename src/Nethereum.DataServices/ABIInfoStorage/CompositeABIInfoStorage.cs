using Nethereum.ABI.ABIRepository;
using Nethereum.ABI.Model;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public class CompositeABIInfoStorage : IABIInfoStorage
    {
        private readonly List<IABIInfoStorage> _storages;

        public CompositeABIInfoStorage(params IABIInfoStorage[] storages)
        {
            _storages = storages?.ToList() ?? new List<IABIInfoStorage>();
        }

        public CompositeABIInfoStorage(IEnumerable<IABIInfoStorage> storages)
        {
            _storages = storages?.ToList() ?? new List<IABIInfoStorage>();
        }

        public void AddABIInfo(ABIInfo abiInfo)
        {
            if (_storages.Count > 0)
            {
                _storages[0].AddABIInfo(abiInfo);
            }
        }

        public ABIInfo GetABIInfo(BigInteger chainId, string contractAddress)
        {
            foreach (var storage in _storages)
            {
                var result = storage.GetABIInfo(chainId, contractAddress);
                if (result != null) return result;
            }
            return null;
        }

        public FunctionABI FindFunctionABI(BigInteger chainId, string contractAddress, string signature)
        {
            foreach (var storage in _storages)
            {
                var result = storage.FindFunctionABI(chainId, contractAddress, signature);
                if (result != null) return result;
            }
            return null;
        }

        public FunctionABI FindFunctionABIFromInputData(BigInteger chainId, string contractAddress, string inputData)
        {
            foreach (var storage in _storages)
            {
                var result = storage.FindFunctionABIFromInputData(chainId, contractAddress, inputData);
                if (result != null) return result;
            }
            return null;
        }

        public EventABI FindEventABI(BigInteger chainId, string contractAddress, string signature)
        {
            foreach (var storage in _storages)
            {
                var result = storage.FindEventABI(chainId, contractAddress, signature);
                if (result != null) return result;
            }
            return null;
        }

        public ErrorABI FindErrorABI(BigInteger chainId, string contractAddress, string signature)
        {
            foreach (var storage in _storages)
            {
                var result = storage.FindErrorABI(chainId, contractAddress, signature);
                if (result != null) return result;
            }
            return null;
        }

        public List<FunctionABI> FindFunctionABI(string signature)
        {
            var results = new List<FunctionABI>();
            foreach (var storage in _storages)
            {
                try
                {
                    var found = storage.FindFunctionABI(signature);
                    if (found != null) results.AddRange(found);
                }
                catch { }
            }
            return results;
        }

        public List<FunctionABI> FindFunctionABIFromInputData(string inputData)
        {
            var results = new List<FunctionABI>();
            foreach (var storage in _storages)
            {
                try
                {
                    var found = storage.FindFunctionABIFromInputData(inputData);
                    if (found != null) results.AddRange(found);
                }
                catch { }
            }
            return results;
        }

        public List<EventABI> FindEventABI(string signature)
        {
            var results = new List<EventABI>();
            foreach (var storage in _storages)
            {
                try
                {
                    var found = storage.FindEventABI(signature);
                    if (found != null) results.AddRange(found);
                }
                catch { }
            }
            return results;
        }

        public List<ErrorABI> FindErrorABI(string signature)
        {
            var results = new List<ErrorABI>();
            foreach (var storage in _storages)
            {
                try
                {
                    var found = storage.FindErrorABI(signature);
                    if (found != null) results.AddRange(found);
                }
                catch { }
            }
            return results;
        }
    }
}
