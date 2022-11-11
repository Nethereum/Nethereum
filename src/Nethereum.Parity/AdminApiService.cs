using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class AdminApiService : RpcClientWrapper, IAdminApiService
    {
        public AdminApiService(IClient client) : base(client)
        {
            ConsensusCapability = new ParityConsensusCapability(client);
            ListOpenedVaults = new ParityListOpenedVaults(client);
            ListVaults = new ParityListVaults(client);
            LocalTransactions = new ParityLocalTransactions(client);
            PendingTransactionsStats = new ParityPendingTransactionsStats(client);
            ReleasesInfo = new ParityReleasesInfo(client);
            VersionInfo = new ParityVersionInfo(client);
            ParityListStorageKeys = new ParityListStorageKeys(client);
        }

        public IParityListStorageKeys   ParityListStorageKeys { get; }
        public IParityConsensusCapability ConsensusCapability { get; }
        public IParityListOpenedVaults ListOpenedVaults { get; }
        public IParityListVaults ListVaults { get; }
        public IParityLocalTransactions LocalTransactions { get; }
        public IParityPendingTransactionsStats PendingTransactionsStats { get; }
        public IParityReleasesInfo ReleasesInfo { get; }
        public IParityVersionInfo VersionInfo { get; }
    }
}