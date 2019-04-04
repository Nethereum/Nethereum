using Nethereum.Contracts.CQS;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
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
        public string ByteCode { get; internal set; }

    }
}