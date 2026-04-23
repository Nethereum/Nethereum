using System.Threading.Tasks;

namespace Nethereum.CoreChain.Proving
{
    public interface IBlockProver
    {
        Task<BlockProofResult> ProveBlockAsync(byte[] witnessBytes,
            byte[] preStateRoot, byte[] postStateRoot, long blockNumber);
    }
}
