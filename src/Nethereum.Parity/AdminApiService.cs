using Nethereum.JsonRpc.Client;
using Nethereum.Parity.RPC.Admin;
using Nethereum.RPC;
using Nethereum.Web3;

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

        public ParityConsensusCapability ConsensusCapability { get; private set; }
        public ParityListOpenedVaults ListOpenedVaults { get; private set; }
        public ParityListVaults ListVaults { get; private set; }
        public ParityLocalTransactions LocalTransactions { get; private set; }
        public ParityPendingTransactionsStats PendingTransactionsStats { get; private set; }
        public ParityReleasesInfo ReleasesInfo { get; private set; }
        public ParityVersionInfo VersionInfo { get; private set; }
            
    }
}