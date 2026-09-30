using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors.Rules
{
    /// <summary>
    /// BLOCKHASH for eth_simulateV1. A simulated block is never written to the block store, so the
    /// committing path's <see cref="LegacyBlockHashRule"/> (a block-store lookup) returns nothing for the
    /// blocks a simulation produced. geth serves BLOCKHASH from an in-memory map that holds both the base
    /// chain's recent hashes and the freshly simulated ones; the equivalent here is: read the block store
    /// for a base block, and fall back to the EIP-2935 history-storage contract (which the pre-transaction
    /// system call has populated with every produced block hash, base and simulated alike) when the store
    /// has nothing. Selected only on the simulate path via
    /// <see cref="TransactionExecutionContext.BlockHashRuleOverride"/>; the committing path is untouched.
    /// </summary>
    public sealed class SimulateBlockHashRule : IBlockHashRule
    {
        public static readonly SimulateBlockHashRule Instance = new SimulateBlockHashRule();
        private SimulateBlockHashRule() { }

        private static bool IsZeroOrNull(byte[] hash)
        {
            if (hash == null || hash.Length == 0) return true;
            for (var i = 0; i < hash.Length; i++)
                if (hash[i] != 0) return false;
            return true;
        }

#if EVM_SYNC
        public byte[] GetBlockHash(Program program, long blockNumber)
        {
            var fromStore = program.ProgramContext.ExecutionStateService.StateReader.GetBlockHash(blockNumber);
            if (!IsZeroOrNull(fromStore)) return fromStore;
            return Eip2935BlockHashRule.Instance.GetBlockHash(program, blockNumber);
        }
#else
        public async Task<byte[]> GetBlockHashAsync(Program program, long blockNumber)
        {
            var fromStore = await program.ProgramContext.ExecutionStateService.StateReader.GetBlockHashAsync(blockNumber);
            if (!IsZeroOrNull(fromStore)) return fromStore;
            return await Eip2935BlockHashRule.Instance.GetBlockHashAsync(program, blockNumber);
        }
#endif
    }
}
