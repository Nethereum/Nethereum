using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Nethereum.RPC;

namespace Nethereum.Parity
{
    public class AdminApiService : RpcClientWrapper
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
        }

        public ParityConsensusCapability ConsensusCapability { get; }
        public ParityListOpenedVaults ListOpenedVaults { get; }
        public ParityListVaults ListVaults { get; }
        public ParityLocalTransactions LocalTransactions { get; }
        public ParityPendingTransactionsStats PendingTransactionsStats { get; }
        public ParityReleasesInfo ReleasesInfo { get; }
        public ParityVersionInfo VersionInfo { get; }
    }
}