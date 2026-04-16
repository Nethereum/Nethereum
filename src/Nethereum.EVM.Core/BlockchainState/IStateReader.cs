using Nethereum.Util;
#if !EVM_SYNC
using System.Threading.Tasks;
#endif

namespace Nethereum.EVM.BlockchainState
{
    public interface IStateReader
    {
#if EVM_SYNC
        EvmUInt256 GetBalance(byte[] address);
        EvmUInt256 GetBalance(string address);
        byte[] GetCode(byte[] address);
        byte[] GetCode(string address);
        byte[] GetStorageAt(byte[] address, EvmUInt256 position);
        byte[] GetStorageAt(string address, EvmUInt256 position);
        EvmUInt256 GetTransactionCount(byte[] address);
        EvmUInt256 GetTransactionCount(string address);
#else
        Task<EvmUInt256> GetBalanceAsync(byte[] address);
        Task<EvmUInt256> GetBalanceAsync(string address);
        Task<byte[]> GetCodeAsync(byte[] address);
        Task<byte[]> GetCodeAsync(string address);
        Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position);
        Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position);
        Task<EvmUInt256> GetTransactionCountAsync(byte[] address);
        Task<EvmUInt256> GetTransactionCountAsync(string address);
#endif
    }
}
