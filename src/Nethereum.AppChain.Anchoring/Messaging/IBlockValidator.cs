using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring.Messaging
{
    public interface IBlockValidator
    {
        Task<bool> ValidateBlockAsync(BigInteger blockNumber, byte[] blockHash);
    }
}
