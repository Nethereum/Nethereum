using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors.Rules
{
    /// <summary>
    /// EIP-2935 BLOCKHASH resolution (Prague+): read storage slot
    /// <c>blockNumber % 8191</c> of the history contract at
    /// <c>0x0000F90827F1C53a10cb7A02335B175320002935</c>. The 256-block
    /// window check is done by the executor before this rule is invoked
    /// (storage outside that range is zero by construction).
    /// </summary>
    public sealed class Eip2935BlockHashRule : IBlockHashRule
    {
        public static readonly Eip2935BlockHashRule Instance = new Eip2935BlockHashRule();
        private Eip2935BlockHashRule() { }

        public const string HISTORY_STORAGE_ADDRESS = "0x0000F90827F1C53a10cb7A02335B175320002935";
        public const int HISTORY_SERVE_WINDOW = 8191;

#if EVM_SYNC
        public byte[] GetBlockHash(Program program, long blockNumber)
        {
            var slot = new EvmUInt256(blockNumber % HISTORY_SERVE_WINDOW);
            var blockHash = program.ProgramContext.ExecutionStateService.GetFromStorage(HISTORY_STORAGE_ADDRESS, slot);
            return blockHash ?? new byte[32];
        }
#else
        public async Task<byte[]> GetBlockHashAsync(Program program, long blockNumber)
        {
            var slot = new EvmUInt256(blockNumber % HISTORY_SERVE_WINDOW);
            var blockHash = await program.ProgramContext.ExecutionStateService.GetFromStorageAsync(HISTORY_STORAGE_ADDRESS, slot);
            return blockHash ?? new byte[32];
        }
#endif
    }
}
