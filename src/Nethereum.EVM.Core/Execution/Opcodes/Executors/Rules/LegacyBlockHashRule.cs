#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors.Rules
{
    public sealed class LegacyBlockHashRule : IBlockHashRule
    {
        public static readonly LegacyBlockHashRule Instance = new LegacyBlockHashRule();
        private LegacyBlockHashRule() { }

#if EVM_SYNC
        public byte[] GetBlockHash(Program program, long blockNumber)
        {
            var hash = program.ProgramContext.ExecutionStateService.StateReader.GetBlockHash(blockNumber);
            return hash ?? new byte[32];
        }
#else
        public async Task<byte[]> GetBlockHashAsync(Program program, long blockNumber)
        {
            var hash = await program.ProgramContext.ExecutionStateService.StateReader.GetBlockHashAsync(blockNumber);
            return hash ?? new byte[32];
        }
#endif
    }
}
