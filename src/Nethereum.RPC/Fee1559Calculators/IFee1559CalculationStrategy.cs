using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.RPC.Fee1559Calculators
{
#if !DOTNET35
    public interface IFee1559CalculationStrategy
    {
        Task<Fee1559> CalculateFee(BigInteger? maxPriorityFeePerGas = null);
    }
#endif
}