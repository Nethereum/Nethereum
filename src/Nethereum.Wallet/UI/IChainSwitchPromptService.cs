using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public interface IChainSwitchPromptService
    {
        Task<bool> RequestSwitchAsync(ChainSwitchPromptRequest request);
    }
}
