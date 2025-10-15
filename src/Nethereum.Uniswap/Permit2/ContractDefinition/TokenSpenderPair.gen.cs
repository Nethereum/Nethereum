using Nethereum.ABI.FunctionEncoding.Attributes;

namespace Nethereum.Uniswap.Permit2.ContractDefinition
{
    public partial class TokenSpenderPair : TokenSpenderPairBase { }

    public class TokenSpenderPairBase 
    {
        [Parameter("address", "token", 1)]
        public virtual string Token { get; set; }
        [Parameter("address", "spender", 2)]
        public virtual string Spender { get; set; }
    }
}
