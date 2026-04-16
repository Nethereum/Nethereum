#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public interface IOpcodeGasCostAsync
    {
#if EVM_SYNC
        long GetGasCost(Program program);
#else
        Task<long> GetGasCostAsync(Program program);
#endif
    }
}
