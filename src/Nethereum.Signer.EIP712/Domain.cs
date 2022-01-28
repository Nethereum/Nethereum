using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Numerics;

namespace Nethereum.Signer.EIP712
{
 
    [Struct("EIP712Domain")]
    public class Domain
    {
        [Parameter("string", "name", 1)]
        public virtual string Name { get; set; }

        [Parameter("string", "version", 2)]
        public virtual string Version { get; set; }

        [Parameter("uint256", "chainId", 3)]
        public virtual BigInteger? ChainId { get; set; }

        [Parameter("address", "verifyingContract", 4)]
        public virtual string VerifyingContract { get; set; }
       
    }

    [Struct("EIP712Domain")]
    public class DomainWithSalt:Domain
    {
        [Parameter("bytes32", "salt", 5)]
        public virtual byte[] Salt { get; set; }
    }
}