using Nethereum.RPC.Infrastructure;

namespace Nethereum.Pantheon.RPC.Permissioning
{
    public interface IPermGetAccountsWhitelist : IGenericRpcRequestResponseHandlerNoParam<string[]>
    {
    }
}