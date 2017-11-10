using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionManagers;
using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    public abstract class ContractHandlerBase<TContractMessage> where TContractMessage : ContractMessage
    {
        public EthApiContractService Eth { get; protected set; }

        public void Initialise(IClient client, ITransactionManager transactionManager)
        {
            this.Eth = new EthApiContractService(client, transactionManager);
        }

        public void Initialise(EthApiContractService ethApiContractService)
        {
            this.Eth = ethApiContractService;
        }

        protected virtual HexBigInteger GetMaximumGas(TContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.Gas);
        }

        protected virtual HexBigInteger GetValue(TContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.AmountToSend);
        }

        protected virtual HexBigInteger GetGasPrice(TContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.GasPrice);
        }

        protected HexBigInteger GetDefaultValue(BigInteger? bigInteger)
        {
            return bigInteger == null ? null : new HexBigInteger(bigInteger.Value);
        }

        protected virtual void ValidateContractMessage(TContractMessage contractMessage)
        {
            //check attribute type?
        }

    }
}
