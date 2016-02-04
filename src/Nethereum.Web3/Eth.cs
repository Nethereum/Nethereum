using edjCase.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.Compilation;
using Nethereum.RPC.Eth.Filters;
using Nethereum.RPC.Eth.Transactions;
using Nethereum.RPC.Generic;

namespace Nethereum.Web3
{
    public class Eth
    {
        private RpcClient client;

        //TODO cascade changes
        public string DefaultAccount { get; set; }
        public BlockParameter DefaultBlock { get; set; }

        public Eth(RpcClient client)
        {
            this.client = client;
            this.DefaultBlock = BlockParameter.CreateLatest();

            DeployContract = new DeployContract(client);

            Syncing = new EthSyncing(client);
            //todo isSyncing (watch pull thread)
            CoinBase = new EthCoinBase(client);
            Mining = new EthMining(client);
            Hashrate = new EthHashrate(client);
            GasPrice = new EthGasPrice(client);
            Accounts = new EthAccounts(client);
            BlockNumber = new EthBlockNumber(client);
            GetBalance = new EthGetBalance(client);
            GetStorageAt = new EthGetStorageAt(client);
            GetCode = new EthGetCode(client);


            EstimateGas = new EthEstimateGas(client);

            GetBlockWithTransactionsByHash = new EthGetBlockWithTransactionsByHash(client);
            GetBlockWithTransactionsHashesByHash = new EthGetBlockWithTransactionsHashesByHash(client);
            GetBlockTransactionCountByHash = new EthGetBlockTransactionCountByHash(client);
            GetBlockTransactionCountByNumber = new EthGetBlockTransactionCountByNumber(client);


            GetTransactionByBlockHashAndIndex = new EthGetTransactionByBlockHashAndIndex(client);
            GetTransactionByBlockNumberAndIndex = new EthGetTransactionByBlockNumberAndIndex(client);
            GetTransactionByHash = new EthGetTransactionByHash(client);
            GetTransactionCount = new EthGetTransactionCount(client);
            GetUncleCountByBlockHash = new EthGetUncleCountByBlockHash(client);
            GetUncleCountByBlockNumber = new EthGetUncleCountByBlockNumber(client);


            ProtocolVersion = new EthProtocolVersion(client);
            SendRawTransaction = new EthSendRawTransaction(client);
            Sign = new EthSign(client);

            Call = new EthCall(client);
            GetTransactionReceipt = new EthGetTransactionReceipt(client);
            SendTransaction = new EthSendTransaction(client);
            GetFilterChangesForEthNewFilter = new EthGetFilterChangesForEthNewFilter(client);
            NewBlockFilter = new EthNewBlockFilter(client);
            NewFilter = new EthNewFilter(client);
            NewPendingTransactionFilter = new EthNewPendingTransactionFilter(client);
            CompileSolidity = new EthCompileSolidity(client);
            GetCompilers = new EthGetCompilers(client);

        }

        public EthAccounts Accounts { get; private set; }
        public EthBlockNumber BlockNumber { get; private set; }
        public EthCoinBase CoinBase { get; private set; }
        public EthEstimateGas EstimateGas { get; private set; }
        public EthGasPrice GasPrice { get; private set; }
        public EthGetBalance GetBalance { get; private set; }
        public EthGetBlockWithTransactionsByHash GetBlockWithTransactionsByHash { get; private set; }
        public EthGetBlockWithTransactionsHashesByHash GetBlockWithTransactionsHashesByHash { get; private set; }
        public EthGetBlockTransactionCountByHash GetBlockTransactionCountByHash { get; private set; }
        public EthGetBlockTransactionCountByNumber GetBlockTransactionCountByNumber { get; private set; }
        public EthGetCode GetCode { get; private set; }
        public EthGetStorageAt GetStorageAt { get; private set; }
        public EthGetTransactionByBlockHashAndIndex GetTransactionByBlockHashAndIndex { get; private set; }
        public EthGetTransactionByBlockNumberAndIndex GetTransactionByBlockNumberAndIndex { get; private set; }
        public EthGetTransactionByHash GetTransactionByHash { get; private set; }
        public EthGetTransactionCount GetTransactionCount { get; private set; }
        public EthGetUncleCountByBlockHash GetUncleCountByBlockHash { get; private set; }
        public EthGetUncleCountByBlockNumber GetUncleCountByBlockNumber { get; private set; }
        public EthHashrate Hashrate { get; private set; }
        public EthMining Mining { get; private set; }
        public EthProtocolVersion ProtocolVersion { get; private set; }
        public EthSendRawTransaction SendRawTransaction { get; private set; }
        public EthSign Sign { get; private set; }
        public EthSyncing Syncing { get; private set; }
        public EthCall Call { get; private set; }
        public EthGetTransactionReceipt GetTransactionReceipt { get; private set; }
        public EthSendTransaction SendTransaction { get; private set; }
        public EthGetFilterChangesForEthNewFilter GetFilterChangesForEthNewFilter { get; private set; }
        public EthNewBlockFilter NewBlockFilter { get; private set; }
        public EthNewFilter NewFilter { get; private set; }
        public EthNewPendingTransactionFilter NewPendingTransactionFilter { get; private set; }
        public EthCompileSolidity CompileSolidity { get; private set; }
        public EthGetCompilers GetCompilers { get; private set; }

        public DeployContract DeployContract { get; private set; }

        public Contract GetContract(string abi, string contractAddress)
        {
            var contract = new Contract(client, abi, contractAddress);
            contract.DefaultBlock = DefaultBlock;
            contract.DefaultAccount = DefaultAccount;
            return contract;
        }

        public Contract GetContract(string contractAddress)
        {
            var contract = new Contract(client, contractAddress);
            contract.DefaultBlock = DefaultBlock;
            contract.DefaultAccount = DefaultAccount;
            return contract;
        }
    }
}