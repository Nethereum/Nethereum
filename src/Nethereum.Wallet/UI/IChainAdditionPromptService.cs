using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface IChainAdditionPromptService
    {
        Task<ChainAdditionPromptResult> RequestAddChainAsync(ChainAdditionPromptRequest request);
    }
}
