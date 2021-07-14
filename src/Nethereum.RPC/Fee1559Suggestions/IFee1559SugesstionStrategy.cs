using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.Fee1559Suggestions
{
#if !DOTNET35
    public interface IFee1559SuggestionStrategy
    {
        Task<Fee1559> SuggestFeeAsync(BigInteger? maxPriorityFeePerGas = null);
    }
#endif
}