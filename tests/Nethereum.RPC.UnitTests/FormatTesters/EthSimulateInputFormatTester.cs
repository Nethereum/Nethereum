using System.Collections.Generic;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.RPC.UnitTests.FormatTesters
{
    public class EthSimulateInputFormatTester
    {
        [Fact]
        public void ShouldSerialiseAndRoundTripEthSimulateInput()
        {
            var input = new EthSimulateInput
            {
                BlockStateCalls = new List<BlockStateCall>
                {
                    new BlockStateCall
                    {
                        BlockOverrides = new BlockOverrides
                        {
                            Number = new HexBigInteger(100)
                        },
                        StateOverrides = new Dictionary<string, AccountOverride>
                        {
                            {
                                "0x1000000000000000000000000000000000000000",
                                new AccountOverride
                                {
                                    Balance = new HexBigInteger(1000)
                                }
                            }
                        },
                        Calls = new List<TransactionInput>
                        {
                            new TransactionInput
                            {
                                From = "0x2000000000000000000000000000000000000000",
                                To = "0x3000000000000000000000000000000000000000",
                                Value = new HexBigInteger(1)
                            }
                        }
                    }
                }
            };

            var json = JsonConvert.SerializeObject(input);

            Assert.Contains("\"blockStateCalls\"", json);
            Assert.Contains("\"blockOverrides\"", json);
            Assert.Contains("\"stateOverrides\"", json);

            var roundTripped = JsonConvert.DeserializeObject<EthSimulateInput>(json);

            Assert.Single(roundTripped.BlockStateCalls);
            Assert.Equal(new HexBigInteger(100).Value, roundTripped.BlockStateCalls[0].BlockOverrides.Number.Value);
            Assert.Equal(new HexBigInteger(1000).Value,
                roundTripped.BlockStateCalls[0].StateOverrides["0x1000000000000000000000000000000000000000"].Balance.Value);
            Assert.Single(roundTripped.BlockStateCalls[0].Calls);
        }
    }
}
