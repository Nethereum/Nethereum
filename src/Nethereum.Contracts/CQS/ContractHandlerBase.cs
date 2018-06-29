using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.CQS
{
    public abstract class ContractHandlerBase<TContractMessage> where TContractMessage : ContractMessage
    {
        public EthApiContractService Eth { get; protected set; }

        public void Initialise(IClient client, IAccount account)
        {
            Eth = new EthApiContractService(client, account.TransactionManager);
        }

        public void Initialise(EthApiContractService ethApiContractService)
        {
            Eth = ethApiContractService;
        }

        public virtual string GetAccountAddressFrom()
        {
            return Eth.TransactionManager?.Account?.Address;
        }
    }
}