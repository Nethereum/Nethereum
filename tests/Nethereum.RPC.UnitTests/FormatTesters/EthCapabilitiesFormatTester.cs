using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.RPC.UnitTests.FormatTesters
{
    public class EthCapabilitiesFormatTester
    {
        [Fact]
        public void ShouldSerialiseAndRoundTripEthCapabilitiesResult()
        {
            var result = new EthCapabilitiesResult
            {
                Head = new EthCapabilitiesHead
                {
                    Hash = "0x1000000000000000000000000000000000000000000000000000000000000000",
                    Number = new HexBigInteger(100)
                },
                Tx = new EthCapabilitiesEffectiveResource
                {
                    Disabled = false,
                    OldestBlock = new HexBigInteger(1)
                }
            };

            var json = JsonConvert.SerializeObject(result);

            Assert.Contains("\"head\"", json);
            Assert.Contains("\"tx\"", json);
            Assert.Contains("\"oldestBlock\"", json);

            var roundTripped = JsonConvert.DeserializeObject<EthCapabilitiesResult>(json);

            Assert.Equal(result.Head.Hash, roundTripped.Head.Hash);
            Assert.Equal(result.Head.Number.Value, roundTripped.Head.Number.Value);
            Assert.Equal(result.Tx.Disabled, roundTripped.Tx.Disabled);
            Assert.Equal(result.Tx.OldestBlock.Value, roundTripped.Tx.OldestBlock.Value);
        }

        [Fact]
        public void ShouldSerialiseAndRoundTripEthCapabilitiesDeleteStrategyAsAnObject()
        {
            var result = new EthCapabilitiesResult
            {
                Logs = new EthCapabilitiesEffectiveResource
                {
                    Disabled = false,
                    DeleteStrategy = new EthCapabilitiesDeleteStrategy
                    {
                        Type = "window",
                        RetentionBlocks = new HexBigInteger(100)
                    }
                }
            };

            var json = JsonConvert.SerializeObject(result);

            Assert.Contains("\"deleteStrategy\"", json);
            Assert.Contains("\"retentionBlocks\"", json);

            var roundTripped = JsonConvert.DeserializeObject<EthCapabilitiesResult>(json);

            Assert.IsType<EthCapabilitiesDeleteStrategy>(roundTripped.Logs.DeleteStrategy);
            Assert.Equal(result.Logs.DeleteStrategy.Type, roundTripped.Logs.DeleteStrategy.Type);
            Assert.Equal(result.Logs.DeleteStrategy.RetentionBlocks.Value, roundTripped.Logs.DeleteStrategy.RetentionBlocks.Value);
        }
    }
}
