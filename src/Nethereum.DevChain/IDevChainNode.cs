using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain;

namespace Nethereum.DevChain
{
    public interface IDevChainNode : IChainNode
    {
        Task<byte[]> MineBlockAsync();
        Task<CoreChain.Storage.IStateSnapshot> TakeSnapshotAsync();
        Task RevertToSnapshotAsync(CoreChain.Storage.IStateSnapshot snapshot);
        Task SetBalanceAsync(string address, BigInteger balance);
        Task SetNonceAsync(string address, BigInteger nonce);
        Task SetCodeAsync(string address, byte[] code);
        Task SetStorageAtAsync(string address, BigInteger slot, byte[] value);
    }
}
