using System.Threading.Tasks;

namespace Nethereum.Wallet.UI
{
    public sealed class NoOpChainSwitchPromptService : IChainSwitchPromptService
    {
        public Task<bool> RequestSwitchAsync(ChainSwitchPromptRequest request)
            => Task.FromResult(true);
    }
}
