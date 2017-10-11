using System.Numerics;

namespace Nethereum.Contracts.CQS
{
    public class ContractDeploymentMessage: ContractMessage
    {
        public string ByteCode { get; protected set; }

        public ContractDeploymentMessage(string byteCode)
        {
            this.ByteCode = byteCode;
        }
    }
}
