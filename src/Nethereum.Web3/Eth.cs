using System;
using System.Threading.Tasks;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Transactions;

namespace Nethereum.Web3
{
    public class Eth:RpcClientWrapper
    {
       
        private BlockParameter defaultBlock;

        public BlockParameter DefaultBlock
        {
            get { return defaultBlock; }
            set
            {
                defaultBlock = value;
                SetDefaultBlock();
            }
        }

        private void SetDefaultBlock()
        {
            this.GetBalance.DefaultBlock = DefaultBlock;
            this.GetCode.DefaultBlock = DefaultBlock;
            this.GetStorageAt.DefaultBlock = DefaultBlock;
            this.Transactions.SetDefaultBlock(defaultBlock);

        }

        public Eth(IClient client):base(client)
        {
            this.Client = client;
           
           

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

            this.DefaultBlock = BlockParameter.CreateLatest();

        }

        public EthAccounts Accounts { get; private set; }
        
        public EthCoinBase CoinBase { get; private set; }

        public EthGasPrice GasPrice { get; private set; }
        public EthGetBalance GetBalance { get; private set; }
        
        public EthGetCode GetCode { get; private set; }
        
        public EthGetStorageAt GetStorageAt { get; private set; }
       
        public EthProtocolVersion ProtocolVersion { get; private set; }
         public EthSign Sign { get; private set; }
    
        public EthSyncing Syncing { get; private set; }
        
        public EthTransactionsService Transactions { get; private set; }

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
    }
}