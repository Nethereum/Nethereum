using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.RPC.Tests.FormatTesters
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

            Assert.Equal(callInput.From, "0x1");
            Assert.Equal(callInput.To, "0x2");
            Assert.Equal(callInput.Data, "0x3");

            callInput = new CallInput();
            
            callInput.To = "2";
            callInput.Data = "3";

            Assert.Equal(callInput.From, null);
            Assert.Equal(callInput.To, "0x2");
            Assert.Equal(callInput.Data, "0x3");

            callInput = new CallInput();
            Assert.Equal(callInput.From, null);
            Assert.Equal(callInput.To, null);
            Assert.Equal(callInput.Data, null);
        }
    }
}
