using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Gas.Opcodes.Costs
{
    public sealed class SelfDestructGasCost : IOpcodeGasCostAsync
    {
        private readonly bool _hasColdWarmAccess;

        public SelfDestructGasCost(bool hasColdWarmAccess = true)
        {
            _hasColdWarmAccess = hasColdWarmAccess;
        }

#if EVM_SYNC
        public long GetGasCost(Program program)
#else
        public async Task<long> GetGasCostAsync(Program program)
#endif
        {
            var addressBytes = program.StackPeekAt(0);
            var recipientAddress = addressBytes.ConvertToEthereumChecksumAddress();

            long gas = GasConstants.SELFDESTRUCT_COST;

            if (_hasColdWarmAccess)
            {
                var isWarm = program.IsAddressWarm(addressBytes);
                if (!isWarm)
                {
                    program.MarkAddressAsWarm(addressBytes);
                    gas += GasConstants.COLD_ACCOUNT_ACCESS_COST;
                }
            }

#if EVM_SYNC
            var recipientBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(recipientAddress);
            var recipientCode = program.ProgramContext.ExecutionStateService.GetCode(recipientAddress);
            var recipientNonce = program.ProgramContext.ExecutionStateService.GetNonce(recipientAddress);
#else
            var recipientBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(recipientAddress);
            var recipientCode = await program.ProgramContext.ExecutionStateService.GetCodeAsync(recipientAddress);
            var recipientNonce = await program.ProgramContext.ExecutionStateService.GetNonceAsync(recipientAddress);
#endif
            var accountExists = recipientBalance > 0
                || (recipientCode != null && recipientCode.Length > 0)
                || recipientNonce > 0;

            if (!accountExists)
            {
#if EVM_SYNC
                var selfBalance = program.ProgramContext.ExecutionStateService.GetTotalBalance(program.ProgramContext.AddressContract);
#else
                var selfBalance = await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(program.ProgramContext.AddressContract);
#endif
                if (selfBalance > 0)
                {
                    gas += GasConstants.CALL_NEW_ACCOUNT;
                }
            }

            return gas;
        }

        private static bool IsPrecompileAddress(string address)
        {
            if (string.IsNullOrEmpty(address)) return false;
            var hex = address.StartsWith("0x") || address.StartsWith("0X") ? address.Substring(2) : address;
            var compact = hex.TrimStart('0');
            if (compact.Length == 0) return false;
            if (int.TryParse(compact, System.Globalization.NumberStyles.HexNumber, null, out int addressNum))
            {
                return addressNum >= 1 && addressNum <= 17;
            }
            return false;
        }
    }
}
