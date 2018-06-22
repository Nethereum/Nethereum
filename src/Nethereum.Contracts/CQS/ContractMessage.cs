using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    public static class ContractMessageExtensions
    {
        public static byte[] GetCallData<TContractMessage>(this TContractMessage contractMessage) where TContractMessage: ContractMessage
        {
            var contractBuilder = new ContractBuilder(typeof(TContractMessage), null);
            return contractBuilder.GetFunctionBuilder<TContractMessage>().GetDataAsBytes(contractMessage);
        }
    }


    public class ContractMessage
    {
        public BigInteger AmountToSend { get; set; }
        public BigInteger? Gas { get; set; }
        public BigInteger? GasPrice { get; set; }
        public string FromAddress { get; set; }
        public BigInteger? Nonce { get; set; }
    }
}