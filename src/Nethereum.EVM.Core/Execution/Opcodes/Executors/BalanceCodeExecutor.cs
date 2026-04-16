#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class BalanceCodeExecutor : IOpcodeExecutorAsync
    {
        private readonly EvmBlockchainCurrentContractContextExecution _context;
        private readonly EvmCodeExecution _code;
        private readonly bool _hasSelfBalance;
        private readonly bool _hasExtCodeHash;

        public BalanceCodeExecutor(
            EvmBlockchainCurrentContractContextExecution context,
            EvmCodeExecution code,
            bool hasSelfBalance = true,
            bool hasExtCodeHash = true)
        {
            _context = context;
            _code = code;
            _hasSelfBalance = hasSelfBalance;
            _hasExtCodeHash = hasExtCodeHash;
        }

#if EVM_SYNC
        public bool Execute(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.BALANCE:
                    _context.Balance(program);
                    return true;
                case Instruction.SELFBALANCE:
                    if (!_hasSelfBalance) return false;
                    _context.SelfBalance(program);
                    return true;
                case Instruction.EXTCODESIZE:
                    _code.ExtCodeSize(program);
                    return true;
                case Instruction.EXTCODECOPY:
                    _code.ExtCodeCopy(program);
                    return true;
                case Instruction.EXTCODEHASH:
                    if (!_hasExtCodeHash) return false;
                    _code.ExtCodeHash(program);
                    return true;
                default:
                    return false;
            }
        }
#else
        public async Task<bool> ExecuteAsync(Instruction opcode, Program program)
        {
            switch (opcode)
            {
                case Instruction.BALANCE:
                    await _context.BalanceAsync(program);
                    return true;
                case Instruction.SELFBALANCE:
                    if (!_hasSelfBalance) return false;
                    await _context.SelfBalanceAsync(program);
                    return true;
                case Instruction.EXTCODESIZE:
                    await _code.ExtCodeSizeAsync(program);
                    return true;
                case Instruction.EXTCODECOPY:
                    await _code.ExtCodeCopyAsync(program);
                    return true;
                case Instruction.EXTCODEHASH:
                    if (!_hasExtCodeHash) return false;
                    await _code.ExtCodeHashAsync(program);
                    return true;
                default:
                    return false;
            }
        }
#endif
    }
}
