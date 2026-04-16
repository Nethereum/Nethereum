#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.CallFrame
{
    public interface ICallFrameInitRule
    {
#if EVM_SYNC
        void Apply(CallFrameSetupContext context);
#else
        Task ApplyAsync(CallFrameSetupContext context);
#endif
    }
}
