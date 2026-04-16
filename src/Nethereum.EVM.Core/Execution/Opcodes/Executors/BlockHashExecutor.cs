using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class BlockHashExecutor : IOpcodeExecutorAsync
    {
        // EIP-2935 final values (active at Prague). BLOCKHASH reads storage slot
        // (blockNumber % 8191) of the history contract. The 256-block window is
        // the visible promise; outside that range the opcode returns zero. Gas
        // stays 20 and the history contract is not warmed under EIP-2929.
        private const string HISTORY_STORAGE_ADDRESS = "0x0000F90827F1C53a10cb7A02335B175320002935";
        private const int HISTORY_SERVE_WINDOW = 8191;
        private const int BLOCKHASH_SERVE_WINDOW = 256;

#if EVM_SYNC
        public bool Execute(Instruction opcode, Program program)
        {
            if (opcode != Instruction.BLOCKHASH) return false;

            var blockNumberU256 = program.StackPopU256();
            if (!blockNumberU256.FitsInULong)
            {
                program.StackPush(new byte[32]);
                program.Step();
                return true;
            }
            var blockNumber = blockNumberU256.ToLongSafe();
            var currentBlock = program.ProgramContext.BlockNumber;

            if (blockNumber >= currentBlock || blockNumber < 0)
            {
                program.StackPush(new byte[32]);
                program.Step();
                return true;
            }

            var blocksAgo = currentBlock - blockNumber;

            if (blocksAgo <= BLOCKHASH_SERVE_WINDOW)
            {
                var slot = new EvmUInt256(blockNumber % HISTORY_SERVE_WINDOW);
                var blockHash = program.ProgramContext.ExecutionStateService.GetFromStorage(HISTORY_STORAGE_ADDRESS, slot);
                program.StackPush(blockHash ?? new byte[32]);
            }
            else
            {
                program.StackPush(new byte[32]);
            }

            program.Step();
            return true;
        }
#else
        public async Task<bool> ExecuteAsync(Instruction opcode, Program program)
        {
            if (opcode != Instruction.BLOCKHASH) return false;

            var blockNumberU256 = program.StackPopU256();
            if (!blockNumberU256.FitsInULong)
            {
                program.StackPush(new byte[32]);
                program.Step();
                return true;
            }
            var blockNumber = blockNumberU256.ToLongSafe();
            var currentBlock = program.ProgramContext.BlockNumber;

            if (blockNumber >= currentBlock || blockNumber < 0)
            {
                program.StackPush(new byte[32]);
                program.Step();
                return true;
            }

            var blocksAgo = currentBlock - blockNumber;

            if (blocksAgo <= BLOCKHASH_SERVE_WINDOW)
            {
                var slot = new EvmUInt256(blockNumber % HISTORY_SERVE_WINDOW);
                var blockHash = await program.ProgramContext.ExecutionStateService.GetFromStorageAsync(HISTORY_STORAGE_ADDRESS, slot);
                program.StackPush(blockHash ?? new byte[32]);
            }
            else
            {
                program.StackPush(new byte[32]);
            }

            program.Step();
            return true;
        }
#endif
    }
}
