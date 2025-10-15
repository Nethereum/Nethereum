using System.Collections.Generic;
using System.Threading.Tasks;

namespace Nethereum.Wallet.Services
{
    public interface IDappPermissionService
    {
        Task<bool> IsApprovedAsync(string origin, string accountAddress);
        Task ApproveAsync(string origin, string accountAddress);
        Task RevokeAsync(string origin, string accountAddress);
        Task<IReadOnlyList<DappPermission>> GetPermissionsAsync(string? accountAddress = null);
    }

    public sealed record DappPermission(string Origin, string AccountAddress, long TimestampUtcSeconds);
}
