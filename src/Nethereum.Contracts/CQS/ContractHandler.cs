using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Accounts;
using Nethereum.RPC.TransactionManagers;
using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    public abstract class ContractHandler<TContractMessage> where TContractMessage : ContractMessage
    {
        public EthApiContractService Eth { get; protected set; }
        public Contract Contract { get; protected set; }

        public void Initialise(IClient client, ITransactionManager transactionManager, string contractAddress)
        {
            this.Eth = new EthApiContractService(client, transactionManager);
            this.Contract = Eth.GetContract<TContractMessage>(contractAddress);
        }

        public Function<TContractMessage> GetFunction()
        { 
            return Contract.GetFunction<TContractMessage>();
        }

        public virtual HexBigInteger GetMaximumGas(TContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.Gas);
        }

        public virtual HexBigInteger GetValue(TContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.AmountToSend);
        }

        public virtual HexBigInteger GetGasPrice(TContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.GasPrice);
        }

        private HexBigInteger GetDefaultValue(BigInteger bigInteger)
        {
            return bigInteger == null ? null : new HexBigInteger(bigInteger);
        }

        public virtual void ValidateFunctionDTO(TContractMessage contractMessage)
        {
            //check attribute type?
        }

    }
}
