using Nethereum.ABI.ABIRepository;
using Nethereum.DataServices.Sourcify;
using System.Collections.Generic;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public static class ABIInfoStorageFactory
    {
        public static IABIInfoStorage CreateDefault(
            string etherscanApiKey = null,
            IABIInfoStorage cache = null)
        {
            var cacheStorage = cache ?? new ABIInfoInMemoryStorage();
            var sourcify = new SourcifyABIInfoStorage();
            var sourcify4Byte = new Sourcify4ByteABIInfoStorage();
            var fourByteDirectory = new FourByteDirectoryABIInfoStorage();

            var implementations = new List<IABIInfoStorage> { sourcify, sourcify4Byte, fourByteDirectory };

            if (!string.IsNullOrEmpty(etherscanApiKey))
            {
                var etherscan = new EtherscanABIInfoStorage(etherscanApiKey);
                implementations.Insert(1, etherscan);
            }

            return new CompositeABIInfoStorage(cacheStorage, implementations.ToArray());
        }

        public static IABIInfoStorage CreateWithSourcifyOnly(IABIInfoStorage cache = null)
        {
            var cacheStorage = cache ?? new ABIInfoInMemoryStorage();
            var sourcify = new SourcifyABIInfoStorage();
            var sourcify4Byte = new Sourcify4ByteABIInfoStorage();
            var fourByteDirectory = new FourByteDirectoryABIInfoStorage();

            return new CompositeABIInfoStorage(cacheStorage, sourcify, sourcify4Byte, fourByteDirectory);
        }

        public static IABIInfoStorage CreateWithEtherscanOnly(
            string etherscanApiKey,
            IABIInfoStorage cache = null)
        {
            var cacheStorage = cache ?? new ABIInfoInMemoryStorage();
            var etherscan = new EtherscanABIInfoStorage(etherscanApiKey);
            var sourcify4Byte = new Sourcify4ByteABIInfoStorage();
            var fourByteDirectory = new FourByteDirectoryABIInfoStorage();

            return new CompositeABIInfoStorage(cacheStorage, etherscan, sourcify4Byte, fourByteDirectory);
        }

        public static IABIInfoStorage CreateLocalOnly(IABIInfoStorage cache = null)
        {
            return cache ?? new ABIInfoInMemoryStorage();
        }

        public static IABIInfoStorage CreateCustom(
            IABIInfoStorage cache,
            params IABIInfoStorage[] implementations)
        {
            var cacheStorage = cache ?? new ABIInfoInMemoryStorage();
            return new CompositeABIInfoStorage(cacheStorage, implementations);
        }
    }
}
