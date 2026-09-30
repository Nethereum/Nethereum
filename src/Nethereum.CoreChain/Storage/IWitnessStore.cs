using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Proving;

namespace Nethereum.CoreChain.Storage
{
    public interface IWitnessStore
    {
        Task StoreWitnessAsync(BigInteger blockNumber, byte[] witnessBytes);
        Task<byte[]> GetWitnessAsync(BigInteger blockNumber);
        Task StoreProofAsync(BigInteger blockNumber, BlockProofResult proof);
        Task<BlockProofResult> GetProofAsync(BigInteger blockNumber);
        Task<IReadOnlyList<BigInteger>> GetUnprovenBlockNumbersAsync();
        Task PurgeWitnessesAsync(WitnessRetentionPolicy policy, BigInteger currentBlock);
    }
}
