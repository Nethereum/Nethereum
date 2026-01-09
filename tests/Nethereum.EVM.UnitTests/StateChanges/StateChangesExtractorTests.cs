using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.EVM.Decoding;
using Nethereum.EVM.StateChanges;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using System.Collections.Generic;
using System.Numerics;
using Xunit;

namespace Nethereum.EVM.UnitTests.StateChanges
{
    public class StateChangesExtractorTests
    {
        private readonly StateChangesExtractor _extractor = new StateChangesExtractor();

        [Fact]
        public void ShouldExtractERC20TransferFromDecodedLog()
        {
            var fromAddress = "0x1234567890123456789012345678901234567890";
            var toAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var amount = BigInteger.Parse("1000000");

            var decodedLog = CreateTransferLog(tokenAddress, fromAddress, toAddress, amount);
            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog> { decodedLog }
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult);

            Assert.False(result.HasError);
            Assert.True(result.HasBalanceChanges);
            Assert.Equal(2, result.BalanceChanges.Count);

            var fromChange = result.BalanceChanges.Find(c => c.Address == fromAddress.ToLowerInvariant());
            var toChange = result.BalanceChanges.Find(c => c.Address == toAddress.ToLowerInvariant());

            Assert.NotNull(fromChange);
            Assert.NotNull(toChange);
            Assert.Equal(-amount, fromChange.Change);
            Assert.Equal(amount, toChange.Change);
            Assert.Equal(BalanceChangeType.ERC20, fromChange.Type);
            Assert.Equal(BalanceChangeType.ERC20, toChange.Type);
            Assert.Equal(tokenAddress, fromChange.TokenAddress);
            Assert.Equal(tokenAddress, toChange.TokenAddress);
        }

        [Fact]
        public void ShouldExtractETHTransferFromCallValue()
        {
            var fromAddress = "0x1234567890123456789012345678901234567890";
            var toAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var ethValue = BigInteger.Parse("1000000000000000000");

            var rootCall = new DecodedCall
            {
                From = fromAddress,
                To = toAddress,
                Value = ethValue,
                IsDecoded = true
            };

            var decodedResult = new DecodedProgramResult
            {
                RootCall = rootCall,
                DecodedLogs = new List<DecodedLog>()
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult);

            Assert.False(result.HasError);
            Assert.True(result.HasBalanceChanges);
            Assert.Equal(2, result.BalanceChanges.Count);

            var fromChange = result.BalanceChanges.Find(c => c.Address == fromAddress.ToLowerInvariant());
            var toChange = result.BalanceChanges.Find(c => c.Address == toAddress.ToLowerInvariant());

            Assert.NotNull(fromChange);
            Assert.NotNull(toChange);
            Assert.Equal(-ethValue, fromChange.Change);
            Assert.Equal(ethValue, toChange.Change);
            Assert.Equal(BalanceChangeType.Native, fromChange.Type);
            Assert.Equal(BalanceChangeType.Native, toChange.Type);
        }

        [Fact]
        public void ShouldConsolidateMultipleTransfersToSameAddress()
        {
            var fromAddress = "0x1234567890123456789012345678901234567890";
            var toAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var amount1 = BigInteger.Parse("1000000");
            var amount2 = BigInteger.Parse("2000000");

            var log1 = CreateTransferLog(tokenAddress, fromAddress, toAddress, amount1);
            var log2 = CreateTransferLog(tokenAddress, fromAddress, toAddress, amount2);

            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog> { log1, log2 }
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult);

            Assert.False(result.HasError);
            Assert.Equal(2, result.BalanceChanges.Count);

            var fromChange = result.BalanceChanges.Find(c => c.Address == fromAddress.ToLowerInvariant());
            var toChange = result.BalanceChanges.Find(c => c.Address == toAddress.ToLowerInvariant());

            Assert.NotNull(fromChange);
            Assert.NotNull(toChange);
            Assert.Equal(-(amount1 + amount2), fromChange.Change);
            Assert.Equal(amount1 + amount2, toChange.Change);
        }

        [Fact]
        public void ShouldMarkCurrentUserAddress()
        {
            var userAddress = "0x1234567890123456789012345678901234567890";
            var otherAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var amount = BigInteger.Parse("1000000");

            var decodedLog = CreateTransferLog(tokenAddress, userAddress, otherAddress, amount);
            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog> { decodedLog }
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult, null, userAddress);

            Assert.False(result.HasError);
            Assert.Equal(2, result.BalanceChanges.Count);

            var userChange = result.BalanceChanges.Find(c => c.Address == userAddress.ToLowerInvariant());
            var otherChange = result.BalanceChanges.Find(c => c.Address == otherAddress.ToLowerInvariant());

            Assert.NotNull(userChange);
            Assert.NotNull(otherChange);
            Assert.True(userChange.IsCurrentUser);
            Assert.False(otherChange.IsCurrentUser);
        }

        [Fact]
        public void ShouldHandleNestedCallsWithMultipleTokens()
        {
            var userAddress = "0x1234567890123456789012345678901234567890";
            var routerAddress = "0x7a250d5630b4cf539739df2c5dacb4c659f2488d";
            var poolAddress = "0x0000000000000000000000000000000000000001";
            var usdcAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var daiAddress = "0x6B175474E89094C44Da98b954EescdeCB5BE3D0";

            var usdcAmount = BigInteger.Parse("100000000");
            var daiAmount = BigInteger.Parse("100000000000000000000");

            var rootCall = new DecodedCall
            {
                From = userAddress,
                To = routerAddress,
                Value = BigInteger.Zero,
                IsDecoded = true,
                InnerCalls = new List<DecodedCall>
                {
                    new DecodedCall { From = routerAddress, To = usdcAddress, Value = BigInteger.Zero },
                    new DecodedCall { From = routerAddress, To = poolAddress, Value = BigInteger.Zero },
                    new DecodedCall { From = poolAddress, To = daiAddress, Value = BigInteger.Zero }
                }
            };

            var usdcTransfer = CreateTransferLog(usdcAddress, userAddress, poolAddress, usdcAmount);
            var daiTransfer = CreateTransferLog(daiAddress, poolAddress, userAddress, daiAmount);

            var decodedResult = new DecodedProgramResult
            {
                RootCall = rootCall,
                DecodedLogs = new List<DecodedLog> { usdcTransfer, daiTransfer }
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult, null, userAddress);

            Assert.False(result.HasError);
            Assert.True(result.HasBalanceChanges);

            var userUsdcChange = result.BalanceChanges.Find(c =>
                c.Address == userAddress.ToLowerInvariant() &&
                c.TokenAddress == usdcAddress);

            var userDaiChange = result.BalanceChanges.Find(c =>
                c.Address == userAddress.ToLowerInvariant() &&
                c.TokenAddress == daiAddress);

            Assert.NotNull(userUsdcChange);
            Assert.NotNull(userDaiChange);
            Assert.Equal(-usdcAmount, userUsdcChange.Change);
            Assert.Equal(daiAmount, userDaiChange.Change);
            Assert.True(userUsdcChange.IsCurrentUser);
            Assert.True(userDaiChange.IsCurrentUser);
        }

        [Fact]
        public void ShouldHandleNullDecodedResult()
        {
            var result = _extractor.ExtractFromDecodedResult(null);

            Assert.True(result.HasError);
            Assert.Equal("No decoded result provided", result.Error);
        }

        [Fact]
        public void ShouldHandleEmptyDecodedResult()
        {
            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog>()
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult);

            Assert.False(result.HasError);
            Assert.False(result.HasBalanceChanges);
            Assert.Empty(result.BalanceChanges);
        }

        [Fact]
        public void ShouldExtractTransferFromRawTopics()
        {
            var fromAddress = "0x1234567890123456789012345678901234567890";
            var toAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var amount = BigInteger.Parse("1000000");

            var log = new DecodedLog
            {
                ContractAddress = tokenAddress,
                IsDecoded = false,
                OriginalLog = new FilterLog
                {
                    Address = tokenAddress,
                    Topics = new object[]
                    {
                        StateChangesExtractor.TRANSFER_EVENT_SIGNATURE,
                        "0x000000000000000000000000" + fromAddress.Substring(2),
                        "0x000000000000000000000000" + toAddress.Substring(2)
                    },
                    Data = "0x" + amount.ToString("X64"),
                    LogIndex = new HexBigInteger(0)
                }
            };

            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog> { log }
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult);

            Assert.False(result.HasError);
            Assert.True(result.HasBalanceChanges);
            Assert.Equal(2, result.BalanceChanges.Count);
        }

        [Fact]
        public void ShouldUseTokenResolver()
        {
            var fromAddress = "0x1234567890123456789012345678901234567890";
            var toAddress = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var amount = BigInteger.Parse("1000000");

            var decodedLog = CreateTransferLog(tokenAddress, fromAddress, toAddress, amount);
            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog> { decodedLog }
            };

            TokenInfo TokenResolver(string address) => new TokenInfo("USDC", 6);

            var result = _extractor.ExtractFromDecodedResult(decodedResult, null, null, TokenResolver);

            Assert.False(result.HasError);
            var change = result.BalanceChanges[0];
            Assert.Equal("USDC", change.TokenSymbol);
            Assert.Equal(6, change.TokenDecimals);
        }

        [Fact]
        public void ShouldFilterOutZeroNetChanges()
        {
            var address1 = "0x1234567890123456789012345678901234567890";
            var address2 = "0xabcdefabcdefabcdefabcdefabcdefabcdefabcd";
            var tokenAddress = "0xA0b86991c6218b36c1d19D4a2e9Eb0cE3606eB48";
            var amount = BigInteger.Parse("1000000");

            var log1 = CreateTransferLog(tokenAddress, address1, address2, amount);
            var log2 = CreateTransferLog(tokenAddress, address2, address1, amount);

            var decodedResult = new DecodedProgramResult
            {
                DecodedLogs = new List<DecodedLog> { log1, log2 }
            };

            var result = _extractor.ExtractFromDecodedResult(decodedResult);

            Assert.False(result.HasError);
            Assert.Empty(result.BalanceChanges);
        }

        private DecodedLog CreateTransferLog(string tokenAddress, string from, string to, BigInteger amount)
        {
            return new DecodedLog
            {
                ContractAddress = tokenAddress,
                Event = new EventABI("Transfer"),
                IsDecoded = true,
                Parameters = new List<ParameterOutput>
                {
                    new ParameterOutput { Parameter = new Parameter("address", "from"), Result = from },
                    new ParameterOutput { Parameter = new Parameter("address", "to"), Result = to },
                    new ParameterOutput { Parameter = new Parameter("uint256", "value"), Result = amount }
                },
                OriginalLog = new FilterLog
                {
                    Address = tokenAddress,
                    Topics = new object[]
                    {
                        StateChangesExtractor.TRANSFER_EVENT_SIGNATURE,
                        "0x000000000000000000000000" + from.Substring(2),
                        "0x000000000000000000000000" + to.Substring(2)
                    },
                    Data = "0x" + amount.ToString("X64"),
                    LogIndex = new HexBigInteger(0)
                }
            };
        }
    }
}
