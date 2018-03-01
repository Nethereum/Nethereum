using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.RPC.UnitTests.FormatTesters
{
    public class CallInputFormatTester
    {
        [Fact]
        public void ShouldForceCorrectFormatForCallInput()
        {
            var callInput = new CallInput();
            callInput.From = "1";
            callInput.To = "2";
            callInput.Data = "3";

            Assert.Equal("0x1", callInput.From);
            Assert.Equal("0x2", callInput.To);
            Assert.Equal("0x3", callInput.Data);

            callInput = new CallInput();
            
            callInput.To = "2";
            callInput.Data = "3";

            Assert.Equal(null, callInput.From);
            Assert.Equal("0x2", callInput.To);
            Assert.Equal("0x3", callInput.Data);

            callInput = new CallInput();
            Assert.Equal(null, callInput.From);
            Assert.Equal(null, callInput.To);
            Assert.Equal(null, callInput.Data);
        }
    }
}
