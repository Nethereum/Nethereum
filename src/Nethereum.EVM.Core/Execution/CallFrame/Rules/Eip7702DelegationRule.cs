using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.CallFrame.Rules
{
    public sealed class Eip7702DelegationRule : ICallFrameInitRule
    {
        public static readonly Eip7702DelegationRule Instance = new Eip7702DelegationRule();

#if EVM_SYNC
        public void Apply(CallFrameSetupContext context)
#else
        public async Task ApplyAsync(CallFrameSetupContext context)
#endif
        {
            var byteCode = context.ByteCode;
            if (!Eip7702DelegationUtils.IsDelegatedCode(byteCode))
                return;

            var delegateAddr = AddressUtil.Current.ConvertToValid20ByteAddress(
                Eip7702DelegationUtils.GetDelegateAddress(byteCode));

            var program = context.Program;
            long delegationAccessGas;
            if (context.ExecutionState.AddressIsWarm(delegateAddr))
            {
                delegationAccessGas = GasConstants.WARM_STORAGE_READ_COST;
            }
            else
            {
                context.ExecutionState.MarkAddressAsWarm(delegateAddr);
                delegationAccessGas = GasConstants.COLD_ACCOUNT_ACCESS_COST;
            }
            program.GasRemaining -= delegationAccessGas;
            program.TotalGasUsed += delegationAccessGas;

#if EVM_SYNC
            context.ByteCode = context.ExecutionState.GetCode(delegateAddr);
#else
            context.ByteCode = await context.ExecutionState.GetCodeAsync(delegateAddr);
#endif
        }
    }
}
