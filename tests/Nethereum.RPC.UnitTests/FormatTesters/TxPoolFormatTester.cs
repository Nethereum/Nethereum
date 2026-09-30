using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.TxPool.DTOs;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.RPC.UnitTests.FormatTesters
{
    public class TxPoolFormatTester
    {
        [Fact]
        public void ShouldSerialiseAndRoundTripTxPoolStatusResponse()
        {
            var status = new TxPoolStatusResponse
            {
                Pending = new HexBigInteger(5),
                Queued = new HexBigInteger(2)
            };

            var json = JsonConvert.SerializeObject(status);

            Assert.Contains("\"pending\"", json);
            Assert.Contains("\"queued\"", json);

            var roundTripped = JsonConvert.DeserializeObject<TxPoolStatusResponse>(json);

            Assert.Equal(status.Pending.Value, roundTripped.Pending.Value);
            Assert.Equal(status.Queued.Value, roundTripped.Queued.Value);
        }

        [Fact]
        public void ShouldSerialiseAndRoundTripTxPoolContentResponse()
        {
            var content = new TxPoolContentResponse
            {
                Pending = new Dictionary<string, Dictionary<string, PendingTransactionInfo>>
                {
                    {
                        "0x1000000000000000000000000000000000000000",
                        new Dictionary<string, PendingTransactionInfo>
                        {
                            {
                                "0",
                                new PendingTransactionInfo
                                {
                                    From = "0x1000000000000000000000000000000000000000",
                                    TransactionHash = "0x2000000000000000000000000000000000000000000000000000000000000000"
                                }
                            }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(content);

            Assert.Contains("\"pending\"", json);
            Assert.DoesNotContain("\"queued\"", json);

            var roundTripped = JsonConvert.DeserializeObject<TxPoolContentResponse>(json);

            var info = roundTripped.Pending["0x1000000000000000000000000000000000000000"]["0"];
            Assert.Equal("0x1000000000000000000000000000000000000000", info.From);
            Assert.Equal("0x2000000000000000000000000000000000000000000000000000000000000000", info.TransactionHash);
        }
    }
}
