using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.RPC
{
    public class EthApiService : RpcClientWrapper
    {
        private BlockParameter defaultBlock;
        private ITransactionManager _transactionManager;

        public EthApiService(IClient client) : this(client, 
            new TransactionManager(client))
        {
           
        }

        public EthApiService(IClient client, ITransactionManager transactionManager) : base(client)
        {
            Client = client;
            Accounts = new EthAccounts(client);
            CoinBase = new EthCoinBase(client);
            GasPrice = new EthGasPrice(client);
            GetBalance = new EthGetBalance(client);
            GetCode = new EthGetCode(client);
            GetStorageAt = new EthGetStorageAt(client);
            ProtocolVersion = new EthProtocolVersion(client);
            Sign = new EthSign(client);
            Syncing = new EthSyncing(client);

            Transactions = new EthApiTransactionsService(client);
            Filters = new EthApiFilterService(client);
            Blocks = new EthApiBlockService(client);
            Uncles = new EthApiUncleService(client);
            Mining = new EthApiMiningService(client);
            Compile = new EthApiCompilerService(client);

            DefaultBlock = BlockParameter.CreateLatest();
            TransactionManager = transactionManager;
            TransactionManager.Client = client; //Ensure is the same
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

        public EthApiTransactionsService Transactions { get; }

        public EthApiUncleService Uncles { get; private set; }
        public EthApiMiningService Mining { get; private set; }
        public EthApiBlockService Blocks { get; private set; }

        public EthApiFilterService Filters { get; private set; }

        public EthApiCompilerService Compile { get; private set; }

        public virtual ITransactionManager TransactionManager
        {
            get { return _transactionManager; }
            set { _transactionManager = value; }
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
