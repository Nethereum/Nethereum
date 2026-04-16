#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.CallFrame.Rules
{
    /// <summary>
    /// EIP-150 (Tangerine Whistle): retain 1/64 of gas on CALL/CREATE.
    /// The caller keeps gasRemaining/64, forwarding the rest to the subcall.
    /// Pre-EIP-150 (Frontier/Homestead): all gas is forwarded.
    /// </summary>
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
