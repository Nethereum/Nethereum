using Nethereum.Web3;

namespace Nethereum.Parity
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "IWeb3Parity")]
    public interface IWeb3Parity: IWeb3
    {
        IAccountsApiService Accounts { get; }
        IAdminApiService Admin { get; }
        IBlockAuthoringApiService BlockAuthoring { get; }
        INetworkApiService Network { get; }
        ITraceApiService Trace { get; }
    }
}