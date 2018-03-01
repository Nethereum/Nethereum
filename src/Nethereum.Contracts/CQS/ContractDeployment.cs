namespace Nethereum.Contracts.CQS
{
    public class ContractDeploymentMessage : ContractMessage
    {
        public ContractDeploymentMessage(string byteCode)
        {
            ByteCode = byteCode;
        }

        public string ByteCode { get; protected set; }
    }
}