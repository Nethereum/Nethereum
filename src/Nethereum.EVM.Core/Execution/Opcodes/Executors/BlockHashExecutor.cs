using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public sealed class BlockHashExecutor : IOpcodeExecutorAsync
    {
        // Visible across every fork; per-fork resolution within the window
        // is delegated to IBlockHashRule.
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
                var blockHash = program.ProgramContext.BlockHashRule.GetBlockHash(program, blockNumber);
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
                var blockHash = await program.ProgramContext.BlockHashRule.GetBlockHashAsync(program, blockNumber);
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
