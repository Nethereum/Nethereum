using System.Collections.Generic;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.CallFrame
{
    public sealed class CallFrameInitRules
    {
        private readonly ICallFrameInitRule[] _rules;

        public CallFrameInitRules(IReadOnlyList<ICallFrameInitRule> rules)
        {
            if (rules == null || rules.Count == 0)
            {
                _rules = new ICallFrameInitRule[0];
                return;
            }
            _rules = new ICallFrameInitRule[rules.Count];
            for (int i = 0; i < rules.Count; i++)
                _rules[i] = rules[i];
        }

#if EVM_SYNC
        public void Apply(CallFrameSetupContext context)
        {
            for (int i = 0; i < _rules.Length; i++)
                _rules[i].Apply(context);
        }
#else
        public async Task ApplyAsync(CallFrameSetupContext context)
        {
            for (int i = 0; i < _rules.Length; i++)
                await _rules[i].ApplyAsync(context);
        }
#endif

        public static readonly CallFrameInitRules Empty = new CallFrameInitRules(new ICallFrameInitRule[0]);
    }
}
