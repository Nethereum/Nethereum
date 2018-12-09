using Nethereum.Web3;

namespace Nethereum.Parity
{
    public interface IWeb3Parity: IWeb3
    {
        IAccountsApiService Accounts { get; }
        IAdminApiService Admin { get; }
        IBlockAuthoringApiService BlockAuthoring { get; }
        INetworkApiService Network { get; }
        ITraceApiService Trace { get; }
    }
}