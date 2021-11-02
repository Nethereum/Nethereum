using Nethereum.ABI.FunctionEncoding.Attributes;
using System.Collections.Generic;
using System.Numerics;
using Xunit;
// ReSharper disable ConsiderUsingConfigureAwait  
// ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

// ReSharper disable AsyncConverter.AsyncWait

namespace Nethereum.Contracts.IntegrationTests.Issues
{
    public class StaticElementArrayTest
    {
        public partial class SwapExactETHForTokensFunction : SwapExactETHForTokensFunctionBase { }

        [Function("swapExactETHForTokens", "uint256[]")]
        public class SwapExactETHForTokensFunctionBase : FunctionMessage
        {
            [Parameter("uint256", "amountOutMin", 1)]
            public virtual BigInteger AmountOutMin { get; set; }
            [Parameter("address[]", "path", 2)]
            public virtual List<string> Path { get; set; }
            [Parameter("address", "to", 3)]
            public virtual string To { get; set; }
            [Parameter("uint256", "deadline", 4)]
            public virtual BigInteger Deadline { get; set; }
        }

        [Fact]
        public void ShouldDecodeEvenIfExtraDataIsIncluded()
        {
            var data = "0x7ff36ab500000000000000000000000000000000000000000000000000000000000f42400000000000000000000000000000000000000000000000000000000000000080000000000000000000000000251366392987f8915badae03c77933bc72682b9300000000000000000000000000000000000000000000000000000000617951f80000000000000000000000000000000000000000000000000000000000000002000000000000000000000000bb4cdb9cbd36b01bd1cbaebf2de08d9173bc095c0000000000000000000000000bc89aa98ad94e6798ec822d0814d934ccd0c0ceffffffffffffffffffffffffffff";
            var decoded = new SwapExactETHForTokensFunction().DecodeInput(data);
        }
    }
}