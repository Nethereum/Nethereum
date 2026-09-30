#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.Execution.Opcodes.Executors
{
    public interface IBlockHashRule
    {
#if EVM_SYNC
        byte[] GetBlockHash(Program program, long blockNumber);
#else
        Task<byte[]> GetBlockHashAsync(Program program, long blockNumber);
#endif
    }
}
