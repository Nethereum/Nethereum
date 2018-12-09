using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Services;
using Nethereum.RPC.TransactionManagers;
using System;

namespace Nethereum.RPC
{
    public class EthApiService : RpcClientWrapper, IEthApiService
    {
        private BlockParameter defaultBlock;
        private ITransactionManager _transactionManager;
        private Lazy<EthSubscriptionService> _subscriptionService;

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

        public IEthAccounts Accounts { get; private set; }

        public IEthCoinBase CoinBase { get; private set; }

        public IEthGasPrice GasPrice { get; private set; }
        public IEthGetBalance GetBalance { get; }

        public IEthGetCode GetCode { get; }

        public IEthGetStorageAt GetStorageAt { get; }

        public IEthProtocolVersion ProtocolVersion { get; private set; }
        public IEthSign Sign { get; private set; }

        public IEthSyncing Syncing { get; private set; }

        public IEthApiTransactionsService Transactions { get; }

        public IEthApiUncleService Uncles { get; private set; }
        public IEthApiMiningService Mining { get; private set; }
        public IEthApiBlockService Blocks { get; private set; }

        public IEthApiFilterService Filters { get; private set; }

        public IEthApiCompilerService Compile { get; private set; }
#if !DOTNET35
        public virtual IEtherTransferService  GetEtherTransferService()
        {
            return new EtherTransferService(TransactionManager);
        }
#endif
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
