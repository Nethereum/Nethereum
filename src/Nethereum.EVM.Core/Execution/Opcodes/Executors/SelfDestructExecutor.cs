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

        public ISelfDestructRule Rule => _rule;

        public SelfDestructExecutor(EvmProgramExecution pe, ISelfDestructRule rule)
        {
            _pe = pe;
            _rule = rule;
        }

        private static void EmitSweepTransferLog(Program program, ref SelfDestructContext ctx, EvmUInt256 contractBalance)
        {
            program.ProgramContext.EthTransferLogRule.Emit(
                program.ProgramResult.Logs, ctx.ContractAddress, ctx.RecipientAddress, contractBalance);
        }

#if EVM_SYNC
        public bool Execute(Instruction opcode, Program program)
        {
            if (opcode != Instruction.SELFDESTRUCT) return false;

            var recipientBytes = program.StackPop();
            var recipientAddress = Nethereum.Util.EvmAddress.From(recipientBytes).ToHexLower();

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

            EmitSweepTransferLog(program, ref ctx, contractBalance);

            program.Stop();
            return true;
        }
#else
        public async Task<bool> ExecuteAsync(Instruction opcode, Program program)
        {
            if (opcode != Instruction.SELFDESTRUCT) return false;

            var recipientBytes = program.StackPop();
            var recipientAddress = Nethereum.Util.EvmAddress.From(recipientBytes).ToHexLower();

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

            EmitSweepTransferLog(program, ref ctx, contractBalance);

            program.Stop();
            return true;
        }
#endif
    }
}
