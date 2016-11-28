using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;

namespace Nethereum.Web3
{
    public class Eth : RpcClientWrapper
    {
        private BlockParameter defaultBlock;

        public Eth(IClient client) : base(client)
        {
            Client = client;


            DeployContract = new DeployContract(client);

            Accounts = new EthAccounts(client);
            CoinBase = new EthCoinBase(client);
            GasPrice = new EthGasPrice(client);
            GetBalance = new EthGetBalance(client);
            GetCode = new EthGetCode(client);
            GetStorageAt = new EthGetStorageAt(client);
            ProtocolVersion = new EthProtocolVersion(client);
            Sign = new EthSign(client);
            Syncing = new EthSyncing(client);

            Transactions = new EthTransactionsService(client);
            Filters = new EthFilterService(client);
            Blocks = new EthBlockService(client);
            Uncles = new EthUncleService(client);
            Mining = new EthMiningService(client);
            Compile = new EthCompilerService(client);

            DefaultBlock = BlockParameter.CreateLatest();
        }

        public BlockParameter DefaultBlock
        {
            get { return defaultBlock; }
            set
            {
                defaultBlock = value;
                SetDefaultBlock();
            }
        }

        public EthAccounts Accounts { get; private set; }

        public EthCoinBase CoinBase { get; private set; }

        public EthGasPrice GasPrice { get; private set; }
        public EthGetBalance GetBalance { get; }

        public EthGetCode GetCode { get; }

        public EthGetStorageAt GetStorageAt { get; }

        public EthProtocolVersion ProtocolVersion { get; private set; }
        public EthSign Sign { get; private set; }

        public EthSyncing Syncing { get; private set; }

        public EthTransactionsService Transactions { get; }

        public EthUncleService Uncles { get; private set; }
        public EthMiningService Mining { get; private set; }
        public EthBlockService Blocks { get; private set; }

        public EthFilterService Filters { get; private set; }

        public EthCompilerService Compile { get; private set; }


        public DeployContract DeployContract { get; private set; }

        public Contract GetContract(string abi, string contractAddress)
        {
            var contract = new Contract(Client, abi, contractAddress);
            contract.DefaultBlock = DefaultBlock;
            return contract;
        }

        private void SetDefaultBlock()
        {
            GetBalance.DefaultBlock = DefaultBlock;
            GetCode.DefaultBlock = DefaultBlock;
            GetStorageAt.DefaultBlock = DefaultBlock;
            Transactions.SetDefaultBlock(defaultBlock);
        }
    }
}