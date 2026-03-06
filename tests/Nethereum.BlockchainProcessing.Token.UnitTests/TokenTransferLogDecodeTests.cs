using System.Numerics;
using Nethereum.BlockchainProcessing.Services.SmartContracts;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ERC1155.ContractDefinition;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using ERC20Transfer = Nethereum.Contracts.Standards.ERC20.ContractDefinition.TransferEventDTO;

namespace Nethereum.BlockchainProcessing.Token.UnitTests
{
    public class TokenTransferLogDecodeTests
    {
        private static readonly string TransferSig =
            (string)Event<ERC20Transfer>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static readonly string TransferSingleSig =
            (string)Event<TransferSingleEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static readonly string TransferBatchSig =
            (string)Event<TransferBatchEventDTO>.GetEventABI().GetTopicBuilder().GetSignatureTopic();

        private static string PadAddress(string address) =>
            "0x000000000000000000000000" + address.Substring(2).ToLowerInvariant();

        private static string PadUint256(BigInteger value) =>
            "0x" + value.ToString("x64");

        [Fact]
        public void DecodeTransferLog_ERC20_DecodesCorrectly()
        {
            var from = "0x1111111111111111111111111111111111111111";
            var to = "0x2222222222222222222222222222222222222222";
            var amount = new BigInteger(1000000);
            var contract = "0xaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";

            var log = new FilterLog
            {
                Address = contract,
                TransactionHash = "0xabc123",
                LogIndex = new HexBigInteger(0),
                BlockNumber = new HexBigInteger(100),
                BlockHash = "0xblockhash",
                Topics = new object[]
                {
                    TransferSig,
                    PadAddress(from),
                    PadAddress(to)
                },
                Data = PadUint256(amount)
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);

            Assert.Single(results);
            var result = results[0];
            Assert.Equal("ERC20", result.TokenType);
            Assert.Equal(from, result.FromAddress);
            Assert.Equal(to, result.ToAddress);
            Assert.Equal(amount.ToString(), result.Amount);
            Assert.Null(result.TokenId);
            Assert.Null(result.OperatorAddress);
            Assert.Equal(contract, result.ContractAddress);
            Assert.Equal("0xabc123", result.TransactionHash);
            Assert.Equal(0L, result.LogIndex);
            Assert.Equal(100L, result.BlockNumber);
            Assert.True(result.IsCanonical);
        }

        [Fact]
        public void DecodeTransferLog_ERC721_DecodesCorrectly()
        {
            var from = "0x1111111111111111111111111111111111111111";
            var to = "0x2222222222222222222222222222222222222222";
            var tokenId = new BigInteger(42);
            var contract = "0xbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

            var log = new FilterLog
            {
                Address = contract,
                TransactionHash = "0xdef456",
                LogIndex = new HexBigInteger(1),
                BlockNumber = new HexBigInteger(200),
                BlockHash = "0xblockhash2",
                Topics = new object[]
                {
                    TransferSig,
                    PadAddress(from),
                    PadAddress(to),
                    PadUint256(tokenId)
                },
                Data = "0x"
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);

            Assert.Single(results);
            var result = results[0];
            Assert.Equal("ERC721", result.TokenType);
            Assert.Equal(from, result.FromAddress);
            Assert.Equal(to, result.ToAddress);
            Assert.Null(result.Amount);
            Assert.Equal(tokenId.ToString(), result.TokenId);
            Assert.Null(result.OperatorAddress);
            Assert.Equal(contract, result.ContractAddress);
        }

        [Fact]
        public void DecodeTransferLog_ERC1155TransferSingle_DecodesCorrectly()
        {
            var operatorAddr = "0x3333333333333333333333333333333333333333";
            var from = "0x1111111111111111111111111111111111111111";
            var to = "0x2222222222222222222222222222222222222222";
            var id = new BigInteger(7);
            var value = new BigInteger(50);
            var contract = "0xcccccccccccccccccccccccccccccccccccccccc";

            var data = PadUint256(id).Substring(2) + PadUint256(value).Substring(2);

            var log = new FilterLog
            {
                Address = contract,
                TransactionHash = "0xghi789",
                LogIndex = new HexBigInteger(2),
                BlockNumber = new HexBigInteger(300),
                BlockHash = "0xblockhash3",
                Topics = new object[]
                {
                    TransferSingleSig,
                    PadAddress(operatorAddr),
                    PadAddress(from),
                    PadAddress(to)
                },
                Data = "0x" + data
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);

            Assert.Single(results);
            var result = results[0];
            Assert.Equal("ERC1155", result.TokenType);
            Assert.Equal(from, result.FromAddress);
            Assert.Equal(to, result.ToAddress);
            Assert.Equal(value.ToString(), result.Amount);
            Assert.Equal(id.ToString(), result.TokenId);
            Assert.Equal(operatorAddr, result.OperatorAddress);
            Assert.Equal(contract, result.ContractAddress);
        }

        [Fact]
        public void DecodeTransferLog_ERC1155TransferBatch_ExpandsToMultipleResults()
        {
            var operatorAddr = "0x3333333333333333333333333333333333333333";
            var from = "0x1111111111111111111111111111111111111111";
            var to = "0x2222222222222222222222222222222222222222";
            var contract = "0xdddddddddddddddddddddddddddddddddddddd";

            var ids = new List<BigInteger> { 1, 2, 3 };
            var values = new List<BigInteger> { 10, 20, 30 };

            var abiEncode = new Nethereum.ABI.ABIEncode();
            var encodedData = abiEncode.GetABIEncoded(
                new Nethereum.ABI.ABIValue("uint256[]", ids),
                new Nethereum.ABI.ABIValue("uint256[]", values));
            var dataHex = "0x" + BitConverter.ToString(encodedData).Replace("-", "").ToLowerInvariant();

            var log = new FilterLog
            {
                Address = contract,
                TransactionHash = "0xjkl012",
                LogIndex = new HexBigInteger(3),
                BlockNumber = new HexBigInteger(400),
                BlockHash = "0xblockhash4",
                Topics = new object[]
                {
                    TransferBatchSig,
                    PadAddress(operatorAddr),
                    PadAddress(from),
                    PadAddress(to)
                },
                Data = dataHex
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);

            Assert.Equal(3, results.Count);

            for (int i = 0; i < 3; i++)
            {
                Assert.Equal("ERC1155", results[i].TokenType);
                Assert.Equal(from, results[i].FromAddress);
                Assert.Equal(to, results[i].ToAddress);
                Assert.Equal(ids[i].ToString(), results[i].TokenId);
                Assert.Equal(values[i].ToString(), results[i].Amount);
                Assert.Equal(operatorAddr, results[i].OperatorAddress);
            }
        }

        [Fact]
        public void DecodeTransferLog_NullTopics_ReturnsEmpty()
        {
            var log = new FilterLog
            {
                Address = "0xaaaa",
                Topics = null,
                Data = "0x"
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);
            Assert.Empty(results);
        }

        [Fact]
        public void DecodeTransferLog_EmptyTopics_ReturnsEmpty()
        {
            var log = new FilterLog
            {
                Address = "0xaaaa",
                Topics = Array.Empty<object>(),
                Data = "0x"
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);
            Assert.Empty(results);
        }

        [Fact]
        public void DecodeTransferLog_UnknownEventSig_ReturnsEmpty()
        {
            var log = new FilterLog
            {
                Address = "0xaaaa",
                Topics = new object[] { "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef" },
                Data = "0x"
            };

            var results = TokenTransferLogProcessingService.DecodeTransferLog(log);
            Assert.Empty(results);
        }

        [Fact]
        public void CreateTransferFilterInput_NoAddresses_ReturnsFilterWithTopicsOnly()
        {
            var filter = TokenTransferLogProcessingService.CreateTransferFilterInput();

            Assert.NotNull(filter.Topics);
            Assert.Single(filter.Topics);
            var topicArray = filter.Topics[0] as object[];
            Assert.NotNull(topicArray);
            Assert.Equal(3, topicArray.Length);
            Assert.Null(filter.Address);
        }

        [Fact]
        public void CreateTransferFilterInput_WithAddresses_ReturnsFilterWithAddresses()
        {
            var addresses = new[] { "0xaaa", "0xbbb" };
            var filter = TokenTransferLogProcessingService.CreateTransferFilterInput(addresses);

            Assert.NotNull(filter.Address);
            Assert.Equal(2, filter.Address.Length);
        }
    }
}
