using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;


namespace Nethereum.AccountAbstraction
{
    [Struct("EIP712Domain")]
    public class ERC4337Domain : Domain
    {
        public const string ERC4337_DOMAIN_NAME = "ERC4337";
        public const string ERC4337_DOMAIN_VERSION = "1";

        public ERC4337Domain()
        {
            Name = ERC4337_DOMAIN_NAME;
            Version = ERC4337_DOMAIN_VERSION;
        }

        public ERC4337Domain(string entryPointAddress, BigInteger chainId) : this()
        {
            ChainId = chainId;
            VerifyingContract = entryPointAddress;
        }
    }
}
