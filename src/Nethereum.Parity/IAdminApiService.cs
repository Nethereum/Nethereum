using Nethereum.Parity.RPC.Admin;

namespace Nethereum.Parity
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.ClientExtensions, "parity-client", "IAdminApiService")]
    public interface IAdminApiService
    {
        IParityListStorageKeys ParityListStorageKeys { get; }
        IParityConsensusCapability ConsensusCapability { get; }
        IParityListOpenedVaults ListOpenedVaults { get; }
        IParityListVaults ListVaults { get; }
        IParityLocalTransactions LocalTransactions { get; }
        IParityPendingTransactionsStats PendingTransactionsStats { get; }
        IParityReleasesInfo ReleasesInfo { get; }
        IParityVersionInfo VersionInfo { get; }

    }
}