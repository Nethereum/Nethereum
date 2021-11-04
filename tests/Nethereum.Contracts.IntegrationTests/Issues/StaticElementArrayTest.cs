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

        public partial class UnoswapFunction : UnoswapFunctionBase { }

        [Function("unoswap", "uint256")]
        public class UnoswapFunctionBase : FunctionMessage
        {
            [Parameter("address", "srcToken", 1)]
            public virtual string SrcToken { get; set; }
            [Parameter("uint256", "amount", 2)]
            public virtual BigInteger Amount { get; set; }
            [Parameter("uint256", "minReturn", 3)]
            public virtual BigInteger MinReturn { get; set; }
            [Parameter("bytes32[]", "", 4)]
            public virtual List<byte[]> ReturnValue4 { get; set; }
        }

        [Fact]
        public void ShouldDecode()
        {
            var data = "0x2e95b6c8000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000366ffbc80d1bd500000000000000000000000000000000000000000000000000db85235d983ed80000000000000000000000000000000000000000000000000000000000000080000000000000000000000000000000000000000000000000000000000000000180000000000000003b6d03407ee2d59972dd251f4212cfb69e0414cb33962650e26b9977";
            var decoded = new UnoswapFunction().DecodeInput(data.Substring(10));
        }

    }
}