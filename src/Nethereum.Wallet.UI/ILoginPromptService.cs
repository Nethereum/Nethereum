using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface ILoginPromptService
    {
        Task<bool> PromptLoginAsync();
        Task LogoutAsync();
    }
}
