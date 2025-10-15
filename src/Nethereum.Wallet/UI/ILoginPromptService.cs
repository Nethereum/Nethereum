using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    /// <summary>
    /// Provides a UI hook to unlock / authenticate the wallet (e.g. password, biometric, device unlock).
    /// Returns true if the vault was unlocked / session established, false if the user cancelled.
    /// </summary>
    public interface ILoginPromptService
    {
        Task<bool> PromptLoginAsync();
        Task LogoutAsync();
    }
}