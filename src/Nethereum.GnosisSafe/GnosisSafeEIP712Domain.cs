using System.Numerics;
using Nethereum.ABI.EIP712;
using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.GnosisSafe
{
    [Struct("EIP712Domain")]
    public class GnosisSafeEIP712Domain : IDomain
    {
        [Parameter("uint256", "chainId", 1)]
        public virtual BigInteger? ChainId { get; set; }

        [Parameter("address", "verifyingContract", 2)]
        public virtual string VerifyingContract { get; set; }

    }
}