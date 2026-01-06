using System.Threading.Tasks;

namespace Nethereum.Wallet.Services
{
    public interface IDownloadPromptService
    {
        Task<bool> ConfirmDownloadAsync(string fileName, string? mimeType = null, long? contentLength = null);
        void ShowDownloadSuccess(string fileName);
        void ShowDownloadFailed(string fileName, string? error = null);
    }
}
