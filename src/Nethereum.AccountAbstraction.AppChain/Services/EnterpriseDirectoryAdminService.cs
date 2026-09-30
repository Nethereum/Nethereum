using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.AppChain.Configuration;

namespace Nethereum.AccountAbstraction.AppChain.Services
{
    public class EnterpriseDirectoryAdminService
    {
        private readonly AppChainAccountAdminService _adminService;
        private readonly Func<string, string> _resolveOwnerAddress;
        private readonly Dictionary<string, string> _enrolledAddresses = new Dictionary<string, string>();

        public EnterpriseDirectoryAdminService(AppChainAccountAdminService adminService, Func<string, string> userIdToOwnerAddress)
        {
            _adminService = adminService ?? throw new ArgumentNullException(nameof(adminService));
            _resolveOwnerAddress = userIdToOwnerAddress ?? throw new ArgumentNullException(nameof(userIdToOwnerAddress));
        }

        public EnterpriseDirectoryAdminService(AppChainAccountAdminService adminService, IReadOnlyDictionary<string, string> userIdToOwnerAddress)
            : this(adminService, ResolverFor(userIdToOwnerAddress))
        {
        }

        private static Func<string, string> ResolverFor(IReadOnlyDictionary<string, string> map)
        {
            if (map == null) throw new ArgumentNullException(nameof(map));
            return userId => map.TryGetValue(userId, out var address) ? address : null;
        }

        public async Task<string> EnrollAsync(string userId, BigInteger salt)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("userId is required.", nameof(userId));

            var owner = _resolveOwnerAddress(userId);
            if (string.IsNullOrEmpty(owner))
                throw new InvalidOperationException($"No owner address is mapped for userId '{userId}'.");

            var config = new AppChainAccountConfig
            {
                Owner = owner,
                Salt = salt
            };

            var address = await _adminService.EnrollAccountAsync(config).ConfigureAwait(false);
            _enrolledAddresses[userId] = address;
            return address;
        }

        public Task<string> OffboardBanAsync(string userId, string reason)
        {
            return _adminService.BanUserAsync(ResolveAddress(userId), reason);
        }

        public Task<bool> IsActiveAsync(string userId)
        {
            return _adminService.IsActiveAsync(ResolveAddress(userId));
        }

        public string ResolveAddress(string userId)
        {
            if (string.IsNullOrEmpty(userId))
                throw new ArgumentException("userId is required.", nameof(userId));

            if (_enrolledAddresses.TryGetValue(userId, out var address))
                return address;

            throw new InvalidOperationException($"userId '{userId}' has not been enrolled.");
        }
    }
}
