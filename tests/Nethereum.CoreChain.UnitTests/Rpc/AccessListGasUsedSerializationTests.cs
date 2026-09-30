using System.Collections.Generic;
using System.Text.Json;
using Nethereum.CoreChain.Rpc;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.Rpc
{
    public class AccessListGasUsedSerializationTests
    {
        [Fact]
        public void Given_NoError_When_SerializedForTheWire_Then_ErrorOmitted()
        {
            var dto = new AccessListGasUsed
            {
                AccessList = new List<AccessList>(),
                GasUsed = new HexBigInteger(0x9d51),
                Error = null
            };

            var json = JsonSerializer.Serialize(dto, CoreChainJsonContext.Default.AccessListGasUsed);

            Assert.DoesNotContain("error", json);
            Assert.Contains("gasUsed", json);
        }

        [Fact]
        public void Given_Error_When_SerializedForTheWire_Then_ErrorPresent()
        {
            var dto = new AccessListGasUsed
            {
                AccessList = new List<AccessList>(),
                GasUsed = new HexBigInteger(0x13f9),
                Error = "execution reverted"
            };

            var json = JsonSerializer.Serialize(dto, CoreChainJsonContext.Default.AccessListGasUsed);

            Assert.Contains("execution reverted", json);
        }
    }
}
