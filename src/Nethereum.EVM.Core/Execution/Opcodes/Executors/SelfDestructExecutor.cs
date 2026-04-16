using Nethereum.EVM.Exceptions;
using Nethereum.EVM.Execution.SelfDestruct;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class SelfDestructExecutor : IOpcodeExecutorAsync
    {
        private readonly EvmProgramExecution _pe;
        private readonly ISelfDestructRule _rule;

        public SelfDestructExecutor(EvmProgramExecution pe, ISelfDestructRule rule)
        {
            _pe = pe;
            _rule = rule;
        }

#if EVM_SYNC
        public bool Execute(Instruction opcode, Program program)
        {
            if (opcode != Instruction.SELFDESTRUCT) return false;

            if (program.ProgramContext.IsStatic)
            {
                program.SetExecutionError();
                return true;
            }

            var recipientBytes = program.StackPop();
            var recipientAddress = recipientBytes.ConvertToEthereumChecksumAddress();

            var contractBalance = _pe.BlockchainCurrentContractContext.GetTotalBalance(
                program, program.ProgramContext.AddressContractEncoded);

            program.ProgramContext.ExecutionStateService.GetTotalBalance(recipientAddress);

            var contractAccount = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(
                program.ProgramContext.AddressContract);

            var ctx = new SelfDestructContext
            {
                Program = program,
                ContractAddress = program.ProgramContext.AddressContract,
                RecipientAddress = recipientAddress,
                ContractBalance = contractBalance,
                ContractAccount = contractAccount,
                ExecutionStateService = program.ProgramContext.ExecutionStateService
            };

            _rule.Execute(ref ctx);
            program.Stop();
            return true;
        }
#else
        public async Task<bool> ExecuteAsync(Instruction opcode, Program program)
        {
            if (opcode != Instruction.SELFDESTRUCT) return false;

            if (program.ProgramContext.IsStatic)
                throw new StaticCallViolationException("SELFDESTRUCT");

            var recipientBytes = program.StackPop();
            var recipientAddress = recipientBytes.ConvertToEthereumChecksumAddress();

            var contractBalance = await _pe.BlockchainCurrentContractContext.GetTotalBalanceAsync(
                program, program.ProgramContext.AddressContractEncoded);

            await program.ProgramContext.ExecutionStateService.GetTotalBalanceAsync(recipientAddress);

            var contractAccount = program.ProgramContext.ExecutionStateService.CreateOrGetAccountExecutionState(
                program.ProgramContext.AddressContract);

            var ctx = new SelfDestructContext
            {
                Program = program,
                ContractAddress = program.ProgramContext.AddressContract,
                RecipientAddress = recipientAddress,
                ContractBalance = contractBalance,
                ContractAccount = contractAccount,
                ExecutionStateService = program.ProgramContext.ExecutionStateService
            };

            _rule.Execute(ref ctx);
            program.Stop();
            return true;
        }
#endif
    }
}
