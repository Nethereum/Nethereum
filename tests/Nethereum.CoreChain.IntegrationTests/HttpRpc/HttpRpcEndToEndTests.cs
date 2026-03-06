using System.Numerics;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.Contracts;
using Nethereum.CoreChain.IntegrationTests.Contracts;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.HttpRpc
{
    [Collection("HttpRpc")]
    public class HttpRpcEndToEndTests : IClassFixture<DevChainHttpFixture>
    {
        private readonly DevChainHttpFixture _fixture;
        private static readonly BigInteger OneToken = BigInteger.Parse("1000000000000000000");

        public HttpRpcEndToEndTests(DevChainHttpFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task DeployContract_ReturnsValidReceipt()
        {
            var receipt = await DeployERC20Async();

            Assert.NotNull(receipt);
            Assert.NotNull(receipt.ContractAddress);
            Assert.True(receipt.ContractAddress.Length > 0);
            Assert.Equal(new HexBigInteger(1), receipt.Status);
            Assert.True(receipt.GasUsed.Value > 0);
            Assert.NotNull(receipt.TransactionHash);
            Assert.NotNull(receipt.BlockHash);
            Assert.True(receipt.BlockNumber.Value > 0);
        }

        [Fact]
        public async Task GetTransactionByHash_ReturnsCorrectTx()
        {
            var receipt = await DeployERC20Async();
            var tx = await _fixture.Web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(receipt.TransactionHash);

            Assert.NotNull(tx);
            Assert.Equal(receipt.TransactionHash, tx.TransactionHash);
            Assert.Equal(_fixture.Account.Address.ToLowerInvariant(), tx.From.ToLowerInvariant());
            Assert.NotNull(tx.Input);
            Assert.True(tx.Input.Length > 10);
        }

        [Fact]
        public async Task GetBlockByNumber_ContainsTransactions()
        {
            var receipt = await DeployERC20Async();
            var block = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                receipt.BlockNumber);

            Assert.NotNull(block);
            Assert.True(block.Transactions.Length > 0);
            Assert.True(block.GasUsed.Value > 0);

            var txHashes = block.Transactions.Select(t => t.TransactionHash).ToList();
            Assert.Contains(receipt.TransactionHash, txHashes);
        }

        [Fact]
        public async Task GetBlockByNumber_MatchesGetBlockByHash()
        {
            var receipt = await DeployERC20Async();

            var blockByNumber = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                receipt.BlockNumber);
            var blockByHash = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(
                receipt.BlockHash);

            Assert.NotNull(blockByNumber);
            Assert.NotNull(blockByHash);
            Assert.Equal(blockByNumber.BlockHash, blockByHash.BlockHash);
            Assert.Equal(blockByNumber.Number.Value, blockByHash.Number.Value);
            Assert.Equal(blockByNumber.GasUsed.Value, blockByHash.GasUsed.Value);
            Assert.Equal(blockByNumber.Transactions.Length, blockByHash.Transactions.Length);
            Assert.Equal(blockByNumber.ParentHash, blockByHash.ParentHash);
        }

        [Fact]
        public async Task BlockParentHash_FormsValidChain()
        {
            await DeployERC20Async();
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var latestBlockNum = await _fixture.Web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            Assert.True(latestBlockNum.Value >= 3);

            var currentBlock = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                latestBlockNum);

            for (int i = 0; i < 2; i++)
            {
                Assert.NotNull(currentBlock);
                var parentNum = new HexBigInteger(currentBlock.Number.Value - 1);
                var parentBlock = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                    parentNum);

                Assert.NotNull(parentBlock);
                Assert.Equal(parentBlock.BlockHash, currentBlock.ParentHash);
                currentBlock = parentBlock;
            }
        }

        [Fact]
        public async Task GetTransactionReceipt_HasAllFields()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var transferHandler = _fixture.Web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new TransferFunction { To = DevChainHttpFixture.RecipientAddress, Value = OneToken * 100 });

            Assert.NotNull(transferReceipt);
            Assert.Equal(new HexBigInteger(1), transferReceipt.Status);
            Assert.NotNull(transferReceipt.BlockHash);
            Assert.True(transferReceipt.BlockNumber.Value > 0);
            Assert.True(transferReceipt.TransactionIndex.Value >= 0);
            Assert.True(transferReceipt.GasUsed.Value > 0);
            Assert.True(transferReceipt.CumulativeGasUsed.Value > 0);
        }

        [Fact]
        public async Task EthGetLogs_ReturnsDecodableEvents()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var transferHandler = _fixture.Web3.Eth.GetContractTransactionHandler<TransferFunction>();
            await transferHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new TransferFunction { To = DevChainHttpFixture.RecipientAddress, Value = OneToken * 100 });

            var transferEventSignature = EventExtensions.GetEventABI<TransferEventDTO>().Sha3Signature;
            var filterInput = new NewFilterInput
            {
                Address = new[] { contractAddress },
                Topics = new[] { new object[] { "0x" + transferEventSignature } },
                FromBlock = new BlockParameter(new HexBigInteger(0)),
                ToBlock = BlockParameter.CreateLatest()
            };

            var logs = await _fixture.Web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);

            Assert.NotNull(logs);
            Assert.True(logs.Length > 0, "Expected at least one Transfer log from getLogs");

            var transferLog = logs.Last();
            Assert.Equal(contractAddress.ToLowerInvariant(), transferLog.Address.ToLowerInvariant());
            Assert.NotNull(transferLog.Topics);
            Assert.True(transferLog.Topics.Length >= 3);
            Assert.NotNull(transferLog.Data);
            Assert.NotNull(transferLog.BlockHash);
            Assert.True(transferLog.BlockNumber.Value > 0);
        }

        [Fact]
        public async Task EthGetLogs_FilterByAddress()
        {
            var contractAddress1 = await DeployAndMintAsync(OneToken * 500);
            var contractAddress2 = await DeployAndMintAsync(OneToken * 500);

            var filterInput = new NewFilterInput
            {
                Address = new[] { contractAddress1 },
                FromBlock = new BlockParameter(new HexBigInteger(0)),
                ToBlock = BlockParameter.CreateLatest()
            };

            var logs = await _fixture.Web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);

            Assert.NotNull(logs);
            Assert.All(logs, log =>
                Assert.Equal(contractAddress1.ToLowerInvariant(), log.Address.ToLowerInvariant()));
        }

        [Fact]
        public async Task EthGetLogs_FilterByTopic()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var approveHandler = _fixture.Web3.Eth.GetContractTransactionHandler<ApproveFunction>();
            await approveHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new ApproveFunction { Spender = DevChainHttpFixture.RecipientAddress, Value = OneToken * 500 });

            var transferEventSignature = EventExtensions.GetEventABI<TransferEventDTO>().Sha3Signature;
            var filterInput = new NewFilterInput
            {
                Address = new[] { contractAddress },
                Topics = new[] { new object[] { "0x" + transferEventSignature } },
                FromBlock = new BlockParameter(new HexBigInteger(0)),
                ToBlock = BlockParameter.CreateLatest()
            };

            var logs = await _fixture.Web3.Eth.Filters.GetLogs.SendRequestAsync(filterInput);

            Assert.NotNull(logs);
            Assert.All(logs, log =>
            {
                var topicHex = log.Topics[0].ToString();
                Assert.Equal("0x" + transferEventSignature, topicHex);
            });
        }

        [Fact]
        public async Task EventDecoding_TransferEvent()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var transferHandler = _fixture.Web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var receipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new TransferFunction { To = DevChainHttpFixture.RecipientAddress, Value = OneToken * 42 });

            var transferEvents = receipt.DecodeAllEvents<TransferEventDTO>();

            Assert.Single(transferEvents);
            var evt = transferEvents[0].Event;
            Assert.Equal(_fixture.Account.Address.ToLowerInvariant(), evt.From.ToLowerInvariant());
            Assert.Equal(DevChainHttpFixture.RecipientAddress.ToLowerInvariant(), evt.To.ToLowerInvariant());
            Assert.Equal(OneToken * 42, evt.Value);
        }

        [Fact]
        public async Task EventDecoding_MultipleEventTypes()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 1000);

            var transferHandler = _fixture.Web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new TransferFunction { To = DevChainHttpFixture.RecipientAddress, Value = OneToken * 10 });

            var approveHandler = _fixture.Web3.Eth.GetContractTransactionHandler<ApproveFunction>();
            var approveReceipt = await approveHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new ApproveFunction { Spender = DevChainHttpFixture.RecipientAddress, Value = OneToken * 200 });

            var transferEvents = transferReceipt.DecodeAllEvents<TransferEventDTO>();
            Assert.Single(transferEvents);
            Assert.Equal(OneToken * 10, transferEvents[0].Event.Value);

            var approvalEvents = approveReceipt.DecodeAllEvents<ApprovalEventDTO>();
            Assert.Single(approvalEvents);
            Assert.Equal(OneToken * 200, approvalEvents[0].Event.Value);
            Assert.Equal(DevChainHttpFixture.RecipientAddress.ToLowerInvariant(),
                approvalEvents[0].Event.Spender.ToLowerInvariant());
        }

        [Fact]
        public async Task GetBalance_ReflectsTransfers()
        {
            var senderBalanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(
                _fixture.Account.Address);

            var transferAmount = UnitConversion.Convert.ToWei(1);
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, transferAmount);

            var senderBalanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(
                _fixture.Account.Address);
            var recipientBalance = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(
                DevChainHttpFixture.RecipientAddress);

            Assert.True(senderBalanceAfter.Value < senderBalanceBefore.Value);
            Assert.True(recipientBalance.Value > 0);
        }

        [Fact]
        public async Task GetCode_ReturnsDeployedBytecode()
        {
            var receipt = await DeployERC20Async();
            var code = await _fixture.Web3.Eth.GetCode.SendRequestAsync(receipt.ContractAddress);

            Assert.NotNull(code);
            Assert.True(code.Length > 10, "Deployed bytecode should be non-trivial");
            Assert.StartsWith("0x", code);
        }

        [Fact]
        public async Task EthCall_ReturnsContractState()
        {
            var contractAddress = await DeployAndMintAsync(OneToken * 777);

            var balanceHandler = _fixture.Web3.Eth.GetContractQueryHandler<BalanceOfFunction>();
            var balance = await balanceHandler.QueryAsync<BigInteger>(
                contractAddress,
                new BalanceOfFunction { Account = _fixture.Account.Address });

            Assert.Equal(OneToken * 777, balance);
        }

        [Fact]
        public async Task GetProof_ReturnsValidAccountProof()
        {
            await SendEthTransferAsync(DevChainHttpFixture.RecipientAddress, OneToken);

            var proof = await _fixture.Web3.Eth.GetProof.SendRequestAsync(
                _fixture.Account.Address,
                Array.Empty<string>(),
                BlockParameter.CreateLatest());

            Assert.NotNull(proof);
            Assert.NotNull(proof.AccountProofs);
            Assert.True(proof.AccountProofs.Count > 0);
            Assert.True(proof.Balance.Value > 0);
        }

        [Fact]
        public async Task BlockHashConsistency_AcrossOperations()
        {
            var deployReceipt = await DeployERC20Async();
            var contractAddress = deployReceipt.ContractAddress;

            var mintHandler = _fixture.Web3.Eth.GetContractTransactionHandler<MintFunction>();
            var mintReceipt = await mintHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new MintFunction { To = _fixture.Account.Address, Amount = OneToken * 500 });

            var transferHandler = _fixture.Web3.Eth.GetContractTransactionHandler<TransferFunction>();
            var transferReceipt = await transferHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new TransferFunction { To = DevChainHttpFixture.RecipientAddress, Value = OneToken * 50 });

            var receipts = new[] { deployReceipt, mintReceipt, transferReceipt };
            foreach (var receipt in receipts)
            {
                var blockByNum = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(
                    receipt.BlockNumber);
                Assert.NotNull(blockByNum);
                Assert.Equal(receipt.BlockHash, blockByNum.BlockHash);

                var blockByHash = await _fixture.Web3.Eth.Blocks.GetBlockWithTransactionsByHash.SendRequestAsync(
                    receipt.BlockHash);
                Assert.NotNull(blockByHash);
                Assert.Equal(blockByNum.Number.Value, blockByHash.Number.Value);
                Assert.Equal(blockByNum.BlockHash, blockByHash.BlockHash);
                Assert.Equal(blockByNum.ParentHash, blockByHash.ParentHash);
                Assert.Equal(blockByNum.Transactions.Length, blockByHash.Transactions.Length);
            }

            var balance = await _fixture.Web3.Eth.GetContractQueryHandler<BalanceOfFunction>()
                .QueryAsync<BigInteger>(contractAddress,
                    new BalanceOfFunction { Account = DevChainHttpFixture.RecipientAddress });
            Assert.Equal(OneToken * 50, balance);
        }

        private async Task<TransactionReceipt> DeployERC20Async()
        {
            return await _fixture.Web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                ERC20Contract.BYTECODE,
                _fixture.Account.Address,
                new HexBigInteger(3000000));
        }

        private async Task<string> DeployAndMintAsync(BigInteger mintAmount)
        {
            var receipt = await DeployERC20Async();
            var contractAddress = receipt.ContractAddress;

            var mintHandler = _fixture.Web3.Eth.GetContractTransactionHandler<MintFunction>();
            await mintHandler.SendRequestAndWaitForReceiptAsync(
                contractAddress,
                new MintFunction { To = _fixture.Account.Address, Amount = mintAmount });

            return contractAddress;
        }

        private async Task<TransactionReceipt> SendEthTransferAsync(string to, BigInteger value)
        {
            var txInput = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = _fixture.Account.Address,
                To = to,
                Value = new HexBigInteger(value),
                Gas = new HexBigInteger(21000)
            };
            return await _fixture.Web3.Eth.TransactionManager.SendTransactionAndWaitForReceiptAsync(txInput);
        }
    }
}
