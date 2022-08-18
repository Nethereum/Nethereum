using Nethereum.RPC.Infrastructure;

namespace Nethereum.RPC.HostWallet
{
    /// <summary>
    /// EIP-1102 https://eips.ethereum.org/EIPS/eip-1102
    /// Requests that the user provides an Ethereum address to be identified by.
    /// </summary>
    public interface IEthRequestAccounts : IGenericRpcRequestResponseHandlerNoParam<string[]>
    {

    }
}
