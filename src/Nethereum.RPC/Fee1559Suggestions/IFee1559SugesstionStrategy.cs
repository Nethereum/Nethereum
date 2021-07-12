using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.Fee1559Suggestions
{
#if !DOTNET35
    public interface IFee1559SugesstionStrategy
    {
        Task<Fee1559> SuggestFee(BigInteger? maxPriorityFeePerGas = null);
    }
#endif
}