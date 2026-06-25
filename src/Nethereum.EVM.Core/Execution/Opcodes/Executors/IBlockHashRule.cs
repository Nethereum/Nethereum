#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    /// <summary>
    /// Per-fork BLOCKHASH resolution strategy. Invoked by
    /// <see cref="BlockHashExecutor"/> after the 256-block window + range
    /// boundary checks succeed.
    /// <para><b>Fork variants:</b></para>
    /// <list type="bullet">
    ///   <item>Frontier → Cancun — <see cref="Rules.LegacyBlockHashRule"/>:
    ///   resolve via <c>IStateReader.GetBlockHash(blockNumber)</c> walking
    ///   the block store.</item>
    ///   <item>Prague onwards (EIP-2935) —
    ///   <see cref="Rules.Eip2935BlockHashRule"/>: read storage slot
    ///   <c>blockNumber % 8191</c> of the history contract at
    ///   <c>0x0000F90827F1C53a10cb7A02335B175320002935</c>.</item>
    /// </list>
    /// </summary>
    public interface IBlockHashRule
    {
#if EVM_SYNC
        byte[] GetBlockHash(Program program, long blockNumber);
#else
        Task<byte[]> GetBlockHashAsync(Program program, long blockNumber);
#endif
    }
}
