#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.CallFrame.Rules
{
    public sealed class Eip150GasRetentionRule : ICallFrameInitRule
    {
        public static readonly Eip150GasRetentionRule Instance = new Eip150GasRetentionRule();

#if EVM_SYNC
        public void Apply(CallFrameSetupContext context)
#else
        public Task ApplyAsync(CallFrameSetupContext context)
#endif
        {
            var gasRemaining = context.Program.GasRemaining;
            context.GasToForward = gasRemaining - gasRemaining / 64;
#if !EVM_SYNC
            return Task.FromResult(0);
#endif
        }
    }
}
