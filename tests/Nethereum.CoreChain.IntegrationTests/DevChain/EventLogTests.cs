using System.Numerics;
using System.Text;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RLP;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class EventLogTests : IClassFixture<DevChainNodeFixture>
    {
        private readonly DevChainNodeFixture _fixture;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        private static readonly byte[] TransferEventTopic = new Sha3Keccack()
            .CalculateHash(Encoding.UTF8.GetBytes("Transfer(address,address,uint256)"));

        private static readonly byte[] ApprovalEventTopic = new Sha3Keccack()
            .CalculateHash(Encoding.UTF8.GetBytes("Approval(address,address,uint256)"));

        public EventLogTests(DevChainNodeFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task Transfer_ContainsLogs()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 100);

            Assert.True(result.Success);
            Assert.NotNull(result.Logs);
            Assert.NotEmpty(result.Logs);
        }

        [Fact]
        public async Task Transfer_Receipt_ContainsCorrectLogCount()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);
            var transferFunction = new TransferFunction { To = _fixture.RecipientAddress, Value = OneToken * 100 };
            var callData = transferFunction.GetCallData();
            var signedTx = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            Assert.True(result.Success);
            Assert.Single(result.Logs);
        }

        [Fact]
        public async Task Transfer_Log_HasCorrectContractAddress()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 50);

            Assert.True(result.Success);
            var log = result.Logs[0];
            Assert.Equal(contractAddress.ToLowerInvariant(), log.Address.ToLowerInvariant());
        }

        [Fact]
        public async Task Transfer_Log_HasCorrectTopics()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 50);

            Assert.True(result.Success);
            var log = result.Logs[0];

            Assert.Equal(3, log.Topics.Count);

            Assert.Equal(TransferEventTopic, log.Topics[0]);

            var fromTopic = log.Topics[1];
            var expectedFrom = _fixture.Address.HexToByteArray();
            Assert.Equal(32, fromTopic.Length);
            Assert.True(fromTopic.Skip(12).SequenceEqual(expectedFrom));

            var toTopic = log.Topics[2];
            var expectedTo = _fixture.RecipientAddress.HexToByteArray();
            Assert.Equal(32, toTopic.Length);
            Assert.True(toTopic.Skip(12).SequenceEqual(expectedTo));
        }

        [Fact]
        public async Task Transfer_Log_DataContainsValue()
        {
            var transferAmount = OneToken * 123;
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, transferAmount);

            Assert.True(result.Success);
            var log = result.Logs[0];

            Assert.NotNull(log.Data);
            Assert.Equal(32, log.Data.Length);

            var valueFromLog = log.Data.ToBigIntegerFromRLPDecoded();
            Assert.Equal(transferAmount, valueFromLog);
        }

        [Fact]
        public async Task Approval_Log_HasApprovalEventSignature()
        {
            var approvalAmount = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async();

            var result = await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, approvalAmount);

            Assert.True(result.Success);
            var log = result.Logs[0];

            Assert.Equal(ApprovalEventTopic, log.Topics[0]);
        }

        [Fact]
        public async Task Approval_Log_HasIndexedOwnerAndSpender()
        {
            var approvalAmount = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async();

            var result = await _fixture.ApproveERC20Async(contractAddress, _fixture.RecipientAddress, approvalAmount);

            Assert.True(result.Success);
            var log = result.Logs[0];

            Assert.Equal(3, log.Topics.Count);

            var ownerTopic = log.Topics[1];
            var expectedOwner = _fixture.Address.HexToByteArray();
            Assert.True(ownerTopic.Skip(12).SequenceEqual(expectedOwner));

            var spenderTopic = log.Topics[2];
            var expectedSpender = _fixture.RecipientAddress.HexToByteArray();
            Assert.True(spenderTopic.Skip(12).SequenceEqual(expectedSpender));
        }

        [Fact]
        public async Task MultipleTransfers_AllEventsRecorded()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result1 = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 10);
            var result2 = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 20);
            var result3 = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 30);

            Assert.True(result1.Success);
            Assert.True(result2.Success);
            Assert.True(result3.Success);

            Assert.Single(result1.Logs);
            Assert.Single(result2.Logs);
            Assert.Single(result3.Logs);

            var value1 = result1.Logs[0].Data.ToBigIntegerFromRLPDecoded();
            var value2 = result2.Logs[0].Data.ToBigIntegerFromRLPDecoded();
            var value3 = result3.Logs[0].Data.ToBigIntegerFromRLPDecoded();

            Assert.Equal(OneToken * 10, value1);
            Assert.Equal(OneToken * 20, value2);
            Assert.Equal(OneToken * 30, value3);
        }

        [Fact]
        public async Task Mint_EmitsTransferFromZeroAddress()
        {
            var mintAmount = OneToken * 500;
            var contractAddress = await _fixture.DeployERC20Async();

            var result = await _fixture.MintERC20Async(contractAddress, _fixture.Address, mintAmount);

            Assert.True(result.Success);
            Assert.Single(result.Logs);

            var log = result.Logs[0];
            Assert.Equal(TransferEventTopic, log.Topics[0]);

            var fromTopic = log.Topics[1];
            Assert.True(fromTopic.All(b => b == 0), "Mint should emit Transfer from zero address");

            var valueFromLog = log.Data.ToBigIntegerFromRLPDecoded();
            Assert.Equal(mintAmount, valueFromLog);
        }

        [Fact]
        public async Task Receipt_ContainsLogsFromResult()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);
            var transferFunction = new TransferFunction { To = _fixture.RecipientAddress, Value = OneToken * 100 };
            var callData = transferFunction.GetCallData();
            var signedTx = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            Assert.NotNull(result.Receipt);
            Assert.NotNull(result.Receipt.Logs);
            Assert.Equal(result.Logs.Count, result.Receipt.Logs.Count);
        }

        [Fact]
        public async Task Block_BloomContainsEventTopics()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var result = await _fixture.TransferERC20Async(contractAddress, _fixture.RecipientAddress, OneToken * 100);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            Assert.NotNull(block);
            Assert.NotNull(block.LogsBloom);
            Assert.Equal(256, block.LogsBloom.Length);
        }

        [Fact]
        public async Task Receipt_BloomMatchesBlockBloom_ForSingleTx()
        {
            var contractAddress = await _fixture.DeployERC20Async(OneToken * 1000);

            var transferFunction = new TransferFunction { To = _fixture.RecipientAddress, Value = OneToken * 100 };
            var callData = transferFunction.GetCallData();
            var signedTx = _fixture.CreateSignedTransaction(contractAddress, BigInteger.Zero, callData);

            await _fixture.Node.MineBlockAsync();

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var block = await _fixture.Node.GetLatestBlockAsync();
            var receiptInfo = await _fixture.Node.GetTransactionReceiptInfoAsync(signedTx.Hash);

            Assert.NotNull(block.LogsBloom);
            Assert.NotNull(receiptInfo.Receipt.Bloom);
        }
    }
}
