using System.Threading.Tasks;
using Nethereum.JsonRpc.Client.RpcMessages;

namespace Nethereum.Metamask
{
    public interface IMetamaskInterop
    {
#if NETCOREAPP3_1_OR_GREATER
        ValueTask<string> EnableEthereumAsync();
        ValueTask<bool> CheckMetamaskAvailability();
        ValueTask<string> GetSelectedAddress();
        ValueTask<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
        ValueTask<RpcResponseMessage> SendTransactionAsync(MetamaskRpcRequestMessage rpcRequestMessage);
        ValueTask<string> SignAsync(string utf8Hex);
#else
        Task<string> EnableEthereumAsync();
        Task<bool> CheckMetamaskAvailability();
        Task<string> GetSelectedAddress();
        Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage);
        Task<RpcResponseMessage> SendTransactionAsync(MetamaskRpcRequestMessage rpcRequestMessage);
        Task<string> SignAsync(string utf8Hex);
#endif
    }
}