using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.Filters;

namespace Nethereum.RPC.Eth.Services
{
    public class EthApiFilterService : RpcClientWrapper, IEthApiFilterService
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

        public IEthGetFilterChangesForBlockOrTransaction GetFilterChangesForBlockOrTransaction { get; private set; }
        public IEthGetFilterChangesForEthNewFilter GetFilterChangesForEthNewFilter { get; private set; }
        public IEthGetFilterLogsForBlockOrTransaction GetFilterLogsForBlockOrTransaction { get; private set; }
        public IEthGetFilterLogsForEthNewFilter GetFilterLogsForEthNewFilter { get; private set; }

        public IEthNewBlockFilter NewBlockFilter { get; private set; }
        public IEthNewFilter NewFilter { get; private set; }
        public IEthNewPendingTransactionFilter NewPendingTransactionFilter { get; private set; }

        public IEthUninstallFilter UninstallFilter { get; private set; }
        public IEthGetLogs GetLogs { get; private set; }
    }
}