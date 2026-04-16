#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class SelfDestructFrontierGasCost : IOpcodeGasCostAsync
    {
        public static readonly SelfDestructFrontierGasCost Instance = new SelfDestructFrontierGasCost();

#if EVM_SYNC
        public long GetGasCost(Program program)
        {
            return 0;
        }
#else
        public Task<long> GetGasCostAsync(Program program)
        {
            return Task.FromResult(0L);
        }
#endif
    }
}
