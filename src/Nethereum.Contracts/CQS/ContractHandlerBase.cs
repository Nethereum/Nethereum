using System.Numerics;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.TransactionManagers;

namespace Nethereum.Contracts.CQS
{

    public static class ContractMessageHexBigIntegerExtensions
    {
        public static HexBigInteger GetHexMaximumGas(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.Gas);
        }

        public static HexBigInteger GetHexValue(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.AmountToSend);
        }

        public static HexBigInteger GetHexGasPrice(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.GasPrice);
        }

        public static HexBigInteger GetHexNonce(this ContractMessage contractMessage)
        {
            return GetDefaultValue(contractMessage.Nonce);
        }

        public static HexBigInteger GetDefaultValue(BigInteger? bigInteger)
        {
            return bigInteger == null ? null : new HexBigInteger(bigInteger.Value);
        }
    }


    public abstract class ContractHandlerBase<TContractMessage> where TContractMessage : ContractMessage
    {
        public EthApiContractService Eth { get; protected set; }

        public void Initialise(IClient client, ITransactionManager transactionManager)
        {
            Eth = new EthApiContractService(client, transactionManager);
        }

        public void Initialise(EthApiContractService ethApiContractService)
        {
            Eth = ethApiContractService;
        }

        public Function<TContractMessage> GetFunction(string contractAddress)
        {
            var contract = Eth.GetContract<TContractMessage>(contractAddress);
            var function = contract.GetFunction<TContractMessage>();
            return function;
        }

        protected virtual string GetDefaultAddressFrom(TContractMessage contractMessage)
        {
            if (string.IsNullOrEmpty(contractMessage.FromAddress))
            {
                if (Eth.TransactionManager?.Account != null)
                {
                   return Eth.TransactionManager.Account.Address;
                }
            }
            return contractMessage.FromAddress;
        }

        protected virtual HexBigInteger GetMaximumGas(TContractMessage contractMessage)
        {
            return contractMessage.GetHexMaximumGas();
        }

        protected virtual HexBigInteger GetValue(TContractMessage contractMessage)
        {
            return contractMessage.GetHexValue();
        }

        protected virtual HexBigInteger GetGasPrice(TContractMessage contractMessage)
        {
            return contractMessage.GetHexGasPrice();
        }

        protected virtual HexBigInteger GetNonce(TContractMessage contractMessage)
        {
            return contractMessage.GetHexNonce();
        }

        protected virtual void ValidateContractMessage(TContractMessage contractMessage)
        {
            //check attribute type?
        }
    }
}