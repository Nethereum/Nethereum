using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services
{
    public sealed class PermissiveDappPermissionService : IDappPermissionService
    {
        public Task<bool> IsApprovedAsync(string origin, string accountAddress) => Task.FromResult(true);
        public Task ApproveAsync(string origin, string accountAddress) => Task.CompletedTask;
        public Task RevokeAsync(string origin, string accountAddress) => Task.CompletedTask;
        public Task<IReadOnlyList<DappPermission>> GetPermissionsAsync(string? accountAddress = null)
            => Task.FromResult<IReadOnlyList<DappPermission>>(new List<DappPermission>());
    }
}
