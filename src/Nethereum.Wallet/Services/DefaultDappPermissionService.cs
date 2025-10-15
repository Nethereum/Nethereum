using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Wallet.Storage;

namespace Nethereum.Wallet.Services
{
    public sealed class DefaultDappPermissionService : IDappPermissionService
    {
        private readonly IWalletStorageService _storageService;

        public DefaultDappPermissionService(IWalletStorageService storageService)
        {
            _storageService = storageService;
        }

        public async Task<bool> IsApprovedAsync(string origin, string accountAddress)
        {
            var permissions = await _storageService.GetDappPermissionsAsync(accountAddress).ConfigureAwait(false);
            return permissions.Any(p => string.Equals(p.Origin, origin, StringComparison.OrdinalIgnoreCase));
        }

        public Task ApproveAsync(string origin, string accountAddress)
            => _storageService.AddDappPermissionAsync(accountAddress, origin);

        public Task RevokeAsync(string origin, string accountAddress)
            => _storageService.RemoveDappPermissionAsync(accountAddress, origin);

        public async Task<IReadOnlyList<DappPermission>> GetPermissionsAsync(string? accountAddress = null)
        {
            var permissions = await _storageService.GetDappPermissionsAsync(accountAddress).ConfigureAwait(false);
            return permissions;
        }
    }
}
