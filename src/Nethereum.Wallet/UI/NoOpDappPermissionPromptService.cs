using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public sealed class NoOpDappPermissionPromptService : IDappPermissionPromptService
    {
        public Task<bool> RequestPermissionAsync(DappPermissionPromptRequest request)
            => Task.FromResult(true);
    }
}
