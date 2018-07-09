using Nethereum.Contracts.CQS;

namespace Nethereum.Contracts
{
    public class ContractDeploymentMessage : ContractMessageBase
    {
        public ContractDeploymentMessage(string byteCode)
        {
            ByteCode = byteCode;
        }
        /// <summary>
        /// ByteCode (Compiled code) used for deployment
        /// </summary>
        public string ByteCode { get; protected set; }
    }
}