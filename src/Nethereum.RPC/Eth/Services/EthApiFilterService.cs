using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.Web3
{
    public class EthApiFilterService : RpcClientWrapper
    {
        public EthApiFilterService(IClient client) : base(client)
        {
            GetFilterChangesForBlockOrTransaction = new EthGetFilterChangesForBlockOrTransaction(client);
            GetFilterChangesForEthNewFilter = new EthGetFilterChangesForEthNewFilter(client);
            GetFilterLogsForBlockOrTransaction = new EthGetFilterLogsForBlockOrTransaction(client);
            GetFilterLogsForEthNewFilter = new EthGetFilterLogsForEthNewFilter(client);
            GetLogs = new EthGetLogs(client);
            NewBlockFilter = new EthNewBlockFilter(client);
            NewFilter = new EthNewFilter(client);
            NewPendingTransactionFilter = new EthNewPendingTransactionFilter(client);
            UninstallFilter = new EthUninstallFilter(client);
        }

        public EthGetFilterChangesForBlockOrTransaction GetFilterChangesForBlockOrTransaction { get; private set; }
        public EthGetFilterChangesForEthNewFilter GetFilterChangesForEthNewFilter { get; private set; }
        public EthGetFilterLogsForBlockOrTransaction GetFilterLogsForBlockOrTransaction { get; private set; }
        public EthGetFilterLogsForEthNewFilter GetFilterLogsForEthNewFilter { get; private set; }

        public EthNewBlockFilter NewBlockFilter { get; private set; }
        public EthNewFilter NewFilter { get; private set; }
        public EthNewPendingTransactionFilter NewPendingTransactionFilter { get; private set; }

        public EthUninstallFilter UninstallFilter { get; private set; }
        public EthGetLogs GetLogs { get; private set; }
    }
}