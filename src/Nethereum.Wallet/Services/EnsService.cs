using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.ENS;
using Nethereum.Web3;
using Nethereum.Contracts.Standards.ENS;

namespace Nethereum.Wallet.Services
{
    public class EnsService : IEnsService
    {
        private readonly string _mainnetRpcUrl;

        // Dedicated mainnet Web3 instance for ENS (ENS only exists on mainnet)
        private readonly Lazy<Nethereum.Web3.Web3> _mainnetWeb3;

        // Reused ENS service (now obtained via web3.Eth.GetEnsService)
        private readonly Lazy<ENSService> _ensService;

        private readonly ConcurrentDictionary<string, string?> _addressToNameCache = new();
        private readonly ConcurrentDictionary<string, string?> _nameToAddressCache = new();
        private readonly ConcurrentDictionary<string, DateTime> _cacheTimestamps = new();
        private readonly TimeSpan _cacheDuration = TimeSpan.FromMinutes(15);

        public EnsService(string mainnetRpcUrl)
        {
            _mainnetRpcUrl = mainnetRpcUrl ?? throw new ArgumentNullException(nameof(mainnetRpcUrl));
            _mainnetWeb3 = new Lazy<Nethereum.Web3.Web3>(() => new Nethereum.Web3.Web3(_mainnetRpcUrl));
            _ensService = new Lazy<ENSService>(() => _mainnetWeb3.Value.Eth.GetEnsService());
        }

        public async Task<string?> ResolveAddressToNameAsync(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            var normalizedAddress = address.ToLowerInvariant();

            if (IsCacheValid(normalizedAddress) && _addressToNameCache.TryGetValue(normalizedAddress, out var cachedName))
            {
                return cachedName;
            }

            try
            {
                var ensName = await _ensService.Value.ReverseResolveAsync(address);
                CacheAddressToName(normalizedAddress, ensName);
                return ensName;
            }
            catch (Exception)
            {
                CacheAddressToName(normalizedAddress, null);
            }

            return null;
        }

        public async Task<string?> ResolveNameToAddressAsync(string ensName)
        {
            if (string.IsNullOrEmpty(ensName))
                return null;

            var normalizedName = ensName.ToLowerInvariant();

            if (IsCacheValid(normalizedName) && _nameToAddressCache.TryGetValue(normalizedName, out var cachedAddress))
            {
                return cachedAddress;
            }

            try
            {
                var address = await _ensService.Value.ResolveAddressAsync(ensName);
                CacheNameToAddress(normalizedName, address);
                return address;
            }
            catch (Exception)
            {
                CacheNameToAddress(normalizedName, null);
            }

            return null;
        }

        public async Task<Dictionary<string, string?>> BatchResolveAddressesToNamesAsync(IEnumerable<string> addresses)
        {
            var result = new Dictionary<string, string?>();
            var addressList = addresses.Where(a => !string.IsNullOrEmpty(a)).Distinct().ToList();

            if (!addressList.Any()) return result;

            var uncachedAddresses = new List<string>();
            foreach (var address in addressList)
            {
                var normalizedAddress = address.ToLowerInvariant();
                if (IsCacheValid(normalizedAddress) && _addressToNameCache.TryGetValue(normalizedAddress, out var cachedName))
                {
                    result[address] = cachedName;
                }
                else
                {
                    uncachedAddresses.Add(address);
                }
            }

            if (uncachedAddresses.Any())
            {
                var tasks = uncachedAddresses.Select(async addr =>
                {
                    try
                    {
                        var name = await ResolveAddressToNameAsync(addr);
                        return (addr, name);
                    }
                    catch
                    {
                        return (addr, (string?)null);
                    }
                }).ToArray();

                var results = await Task.WhenAll(tasks);
                foreach (var (addr, name) in results)
                {
                    result[addr] = name;
                }
            }

            return result;
        }

        public void ClearCache()
        {
            _addressToNameCache.Clear();
            _nameToAddressCache.Clear();
            _cacheTimestamps.Clear();
        }

        public string? GetCachedName(string address)
        {
            if (string.IsNullOrEmpty(address))
                return null;

            var normalizedAddress = address.ToLowerInvariant();

            if (IsCacheValid(normalizedAddress) && _addressToNameCache.TryGetValue(normalizedAddress, out var cachedName))
            {
                return cachedName;
            }

            return null;
        }

        private void CacheAddressToName(string address, string? name)
        {
            _addressToNameCache[address] = name;
            _cacheTimestamps[address] = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(name))
            {
                var normalizedName = name.ToLowerInvariant();
                _nameToAddressCache[normalizedName] = address;
                _cacheTimestamps[normalizedName] = DateTime.UtcNow;
            }
        }

        private void CacheNameToAddress(string name, string? address)
        {
            _nameToAddressCache[name] = address;
            _cacheTimestamps[name] = DateTime.UtcNow;

            if (!string.IsNullOrEmpty(address))
            {
                var normalizedAddress = address.ToLowerInvariant();
                _addressToNameCache[normalizedAddress] = name;
                _cacheTimestamps[normalizedAddress] = DateTime.UtcNow;
            }
        }

        private bool IsCacheValid(string key)
        {
            if (!_cacheTimestamps.TryGetValue(key, out var timestamp))
                return false;

            return DateTime.UtcNow - timestamp < _cacheDuration;
        }
    }
}