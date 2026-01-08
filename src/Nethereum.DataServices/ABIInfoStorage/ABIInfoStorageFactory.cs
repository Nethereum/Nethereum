using Nethereum.ABI.ABIRepository;
using Nethereum.DataServices.Sourcify;
using System.Collections.Generic;

namespace Nethereum.DataServices.ABIInfoStorage
{
    public static class ABIInfoStorageFactory
    {
        public static IABIInfoStorage CreateDefault(
            string etherscanApiKey = null,
            ABIInfoInMemoryStorage localStorage = null)
        {
            var local = localStorage ?? new ABIInfoInMemoryStorage();
            var sourcify = new SourcifyABIInfoStorage(new SourcifyApiServiceV2());

            var providers = new List<IABIInfoStorage> { local, sourcify };

            if (!string.IsNullOrEmpty(etherscanApiKey))
            {
                var etherscan = new EtherscanABIInfoStorage(etherscanApiKey);
                providers.Add(etherscan);
            }

            return new CachingABIInfoStorage(
                new CompositeABIInfoStorage(providers),
                local);
        }

        public static IABIInfoStorage CreateWithSourcifyOnly(ABIInfoInMemoryStorage localStorage = null)
        {
            var local = localStorage ?? new ABIInfoInMemoryStorage();
            var sourcify = new SourcifyABIInfoStorage(new SourcifyApiServiceV2());

            return new CachingABIInfoStorage(
                new CompositeABIInfoStorage(local, sourcify),
                local);
        }

        public static IABIInfoStorage CreateWithEtherscanOnly(
            string etherscanApiKey,
            ABIInfoInMemoryStorage localStorage = null)
        {
            var local = localStorage ?? new ABIInfoInMemoryStorage();
            var etherscan = new EtherscanABIInfoStorage(etherscanApiKey);

            return new CachingABIInfoStorage(
                new CompositeABIInfoStorage(local, etherscan),
                local);
        }

        public static IABIInfoStorage CreateLocalOnly(ABIInfoInMemoryStorage localStorage = null)
        {
            return localStorage ?? new ABIInfoInMemoryStorage();
        }

        public static IABIInfoStorage CreateCustom(
            ABIInfoInMemoryStorage localStorage,
            params IABIInfoStorage[] additionalStorages)
        {
            var providers = new List<IABIInfoStorage> { localStorage ?? new ABIInfoInMemoryStorage() };
            if (additionalStorages != null)
            {
                providers.AddRange(additionalStorages);
            }

            return new CachingABIInfoStorage(
                new CompositeABIInfoStorage(providers),
                localStorage);
        }
    }
}
