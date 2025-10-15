using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface IDappPermissionPromptService
    {
        Task<bool> RequestPermissionAsync(DappPermissionPromptRequest request);
    }
}
