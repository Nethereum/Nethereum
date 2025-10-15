using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexTypes;

namespace Nethereum.Wallet.UI
{
    public sealed class NoOpChainAdditionPromptService : IChainAdditionPromptService
    {
        public Task<ChainAdditionPromptResult> RequestAddChainAsync(ChainAdditionPromptRequest request)
        {
            BigInteger? chainId = null;
            try
            {
                if (request.Parameter.ChainId.Value != 0)
                {
                    chainId = new HexBigInteger(request.Parameter.ChainId).Value;
                }
            }
            catch
            {
                chainId = null;
            }

            var result = ChainAdditionPromptResult.ApprovedResult(chainId, request.SwitchAfterAdd, request.SwitchAfterAdd);
            return Task.FromResult(result);
        }
    }
}
