using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    public class ContractDeployment: ContractMessage
    {
        public string ByteCode { get; protected set; }

        public ContractDeployment(string byteCode)
        {
            this.ByteCode = byteCode;
        }
    }
}
