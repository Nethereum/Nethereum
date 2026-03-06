using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor;
using Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor.ContractDefinition;
using Nethereum.DevChain;
using Nethereum.Web3.Accounts;
using Xunit;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class AppChainAnchorContractTests : IAsyncLifetime
    {
        private DevChainNode? _devChain;
        private Web3.Web3? _web3;
        private AppChainAnchorService? _anchorService;
        private string _contractAddress = "";

        private const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _sequencerAddress;
        private const string OtherPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";
        private readonly string _otherAddress;
        private static readonly BigInteger AppChainId = new BigInteger(420420);

        public AppChainAnchorContractTests()
        {
            var sequencerKey = new Nethereum.Signer.EthECKey(SequencerPrivateKey);
            _sequencerAddress = sequencerKey.GetPublicAddress();

            var otherKey = new Nethereum.Signer.EthECKey(OtherPrivateKey);
            _otherAddress = otherKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            var config = new DevChainConfig
            {
                ChainId = 1337,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("100000000000000000000000")
            };
            _devChain = new DevChainNode(config);
            await _devChain.StartAsync(new[] { _sequencerAddress, _otherAddress });

            var account = new Account(SequencerPrivateKey, 1337);
            var rpcClient = new DevChainRpcClient(_devChain, 1337);
            _web3 = new Web3.Web3(account, rpcClient);
            _web3.TransactionManager.UseLegacyAsDefault = true;

            var deployment = new AppChainAnchorDeployment
            {
                AppChainId = AppChainId,
                Sequencer = _sequencerAddress
            };

            var receipt = await AppChainAnchorService.DeployContractAndWaitForReceiptAsync(_web3, deployment);
            _contractAddress = receipt.ContractAddress!;
            _anchorService = new AppChainAnchorService(_web3, _contractAddress);
        }

        public Task DisposeAsync()
        {
            _devChain?.Dispose();
            _devChain = null;
            return Task.CompletedTask;
        }

        [Fact]
        public async Task Deploy_SetsAppChainId()
        {
            var appChainId = await _anchorService!.AppChainIdQueryAsync();

            Assert.Equal(AppChainId, appChainId);
        }

        [Fact]
        public async Task Deploy_SetsSequencer()
        {
            var sequencer = await _anchorService!.SequencerQueryAsync();

            Assert.Equal(_sequencerAddress.ToLowerInvariant(), sequencer.ToLowerInvariant());
        }

        [Fact]
        public async Task Deploy_LatestBlockStartsAtZero()
        {
            var latestBlock = await _anchorService!.LatestBlockQueryAsync();

            Assert.Equal(0, latestBlock);
        }

        [Fact]
        public async Task Anchor_StoresStateRoots()
        {
            var blockNumber = new BigInteger(100);
            var stateRoot = CreateBytes32(1);
            var txRoot = CreateBytes32(2);
            var receiptRoot = CreateBytes32(3);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(blockNumber, stateRoot, txRoot, receiptRoot);

            var anchor = await _anchorService.GetAnchorQueryAsync(blockNumber);

            Assert.Equal(stateRoot, anchor.StateRoot);
            Assert.Equal(txRoot, anchor.TxRoot);
            Assert.Equal(receiptRoot, anchor.ReceiptRoot);
            Assert.True(anchor.Timestamp > 0);
        }

        [Fact]
        public async Task Anchor_UpdatesLatestBlock()
        {
            var blockNumber = new BigInteger(100);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                blockNumber, CreateBytes32(1), CreateBytes32(2), CreateBytes32(3));

            var latestBlock = await _anchorService.LatestBlockQueryAsync();

            Assert.Equal(blockNumber, latestBlock);
        }

        [Fact]
        public async Task Anchor_EmitsAnchoredEvent()
        {
            var blockNumber = new BigInteger(100);
            var stateRoot = CreateBytes32(1);
            var txRoot = CreateBytes32(2);
            var receiptRoot = CreateBytes32(3);

            var receipt = await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                blockNumber, stateRoot, txRoot, receiptRoot);

            var events = receipt.DecodeAllEvents<AnchoredEventDTO>();
            Assert.Single(events);

            var evt = events[0].Event;
            Assert.Equal(blockNumber, evt.BlockNumber);
            Assert.Equal(stateRoot, evt.StateRoot);
            Assert.Equal(txRoot, evt.TxRoot);
            Assert.Equal(receiptRoot, evt.ReceiptRoot);
        }

        [Fact]
        public async Task Anchor_OnlySequencerCanCall()
        {
            var otherAccount = new Account(OtherPrivateKey, 1337);
            var otherRpcClient = new DevChainRpcClient(_devChain!, 1337);
            var otherWeb3 = new Web3.Web3(otherAccount, otherRpcClient);
            otherWeb3.TransactionManager.UseLegacyAsDefault = true;
            var otherAnchorService = new AppChainAnchorService(otherWeb3, _contractAddress);

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await otherAnchorService.AnchorRequestAndWaitForReceiptAsync(
                    new BigInteger(100), CreateBytes32(1), CreateBytes32(2), CreateBytes32(3));
            });
        }

        [Fact]
        public async Task Anchor_RejectsOlderBlockNumber()
        {
            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                new BigInteger(100), CreateBytes32(1), CreateBytes32(2), CreateBytes32(3));

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await _anchorService.AnchorRequestAndWaitForReceiptAsync(
                    new BigInteger(50), CreateBytes32(4), CreateBytes32(5), CreateBytes32(6));
            });
        }

        [Fact]
        public async Task Anchor_RejectsEqualBlockNumber()
        {
            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                new BigInteger(100), CreateBytes32(1), CreateBytes32(2), CreateBytes32(3));

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await _anchorService.AnchorRequestAndWaitForReceiptAsync(
                    new BigInteger(100), CreateBytes32(4), CreateBytes32(5), CreateBytes32(6));
            });
        }

        [Fact]
        public async Task GetAnchor_ReturnsCorrectData()
        {
            var blockNumber1 = new BigInteger(100);
            var stateRoot1 = CreateBytes32(10);
            var txRoot1 = CreateBytes32(11);
            var receiptRoot1 = CreateBytes32(12);

            var blockNumber2 = new BigInteger(200);
            var stateRoot2 = CreateBytes32(20);
            var txRoot2 = CreateBytes32(21);
            var receiptRoot2 = CreateBytes32(22);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(blockNumber1, stateRoot1, txRoot1, receiptRoot1);
            await _anchorService.AnchorRequestAndWaitForReceiptAsync(blockNumber2, stateRoot2, txRoot2, receiptRoot2);

            var anchor1 = await _anchorService.GetAnchorQueryAsync(blockNumber1);
            var anchor2 = await _anchorService.GetAnchorQueryAsync(blockNumber2);

            Assert.Equal(stateRoot1, anchor1.StateRoot);
            Assert.Equal(stateRoot2, anchor2.StateRoot);
        }

        [Fact]
        public async Task GetAnchor_NonExistentBlock_ReturnsEmptyRoots()
        {
            var anchor = await _anchorService!.GetAnchorQueryAsync(new BigInteger(999));

            Assert.Equal(new byte[32], anchor.StateRoot);
            Assert.Equal(new byte[32], anchor.TxRoot);
            Assert.Equal(new byte[32], anchor.ReceiptRoot);
            Assert.Equal(0, anchor.Timestamp);
        }

        [Fact]
        public async Task VerifyAnchor_ReturnsTrueForMatchingAnchor()
        {
            var blockNumber = new BigInteger(100);
            var stateRoot = CreateBytes32(1);
            var txRoot = CreateBytes32(2);
            var receiptRoot = CreateBytes32(3);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(blockNumber, stateRoot, txRoot, receiptRoot);

            var isValid = await _anchorService.VerifyAnchorQueryAsync(blockNumber, stateRoot, txRoot, receiptRoot);

            Assert.True(isValid);
        }

        [Fact]
        public async Task VerifyAnchor_ReturnsFalseForMismatchedRoots()
        {
            var blockNumber = new BigInteger(100);
            var stateRoot = CreateBytes32(1);
            var txRoot = CreateBytes32(2);
            var receiptRoot = CreateBytes32(3);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(blockNumber, stateRoot, txRoot, receiptRoot);

            var wrongStateRoot = CreateBytes32(99);
            var isValid = await _anchorService.VerifyAnchorQueryAsync(blockNumber, wrongStateRoot, txRoot, receiptRoot);

            Assert.False(isValid);
        }

        [Fact]
        public async Task VerifyAnchor_ReturnsFalseForNonExistentBlock()
        {
            var isValid = await _anchorService!.VerifyAnchorQueryAsync(
                new BigInteger(999), CreateBytes32(1), CreateBytes32(2), CreateBytes32(3));

            Assert.False(isValid);
        }

        [Fact]
        public async Task SetSequencer_OnlySequencerCanCall()
        {
            var receipt = await _anchorService!.SetSequencerRequestAndWaitForReceiptAsync(_otherAddress);

            var events = receipt.DecodeAllEvents<SequencerChangedEventDTO>();
            Assert.Single(events);
            Assert.Equal(_sequencerAddress.ToLowerInvariant(), events[0].Event.OldSequencer.ToLowerInvariant());
            Assert.Equal(_otherAddress.ToLowerInvariant(), events[0].Event.NewSequencer.ToLowerInvariant());

            var newSequencer = await _anchorService.SequencerQueryAsync();
            Assert.Equal(_otherAddress.ToLowerInvariant(), newSequencer.ToLowerInvariant());
        }

        [Fact]
        public async Task SetSequencer_NonSequencerCannotCall()
        {
            var otherAccount = new Account(OtherPrivateKey, 1337);
            var otherRpcClient = new DevChainRpcClient(_devChain!, 1337);
            var otherWeb3 = new Web3.Web3(otherAccount, otherRpcClient);
            otherWeb3.TransactionManager.UseLegacyAsDefault = true;
            var otherAnchorService = new AppChainAnchorService(otherWeb3, _contractAddress);

            await Assert.ThrowsAsync<SmartContractRevertException>(async () =>
            {
                await otherAnchorService.SetSequencerRequestAndWaitForReceiptAsync(_otherAddress);
            });
        }

        [Fact]
        public async Task MultipleAnchors_AllStored()
        {
            for (int i = 1; i <= 5; i++)
            {
                var blockNumber = new BigInteger(i * 100);
                await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                    blockNumber, CreateBytes32(i), CreateBytes32(i + 10), CreateBytes32(i + 20));
            }

            var latestBlock = await _anchorService!.LatestBlockQueryAsync();
            Assert.Equal(500, latestBlock);

            for (int i = 1; i <= 5; i++)
            {
                var blockNumber = new BigInteger(i * 100);
                var anchor = await _anchorService.GetAnchorQueryAsync(blockNumber);
                Assert.Equal(CreateBytes32(i), anchor.StateRoot);
            }
        }

        private static byte[] CreateBytes32(int seed)
        {
            var result = new byte[32];
            result[0] = (byte)(seed & 0xFF);
            result[1] = (byte)((seed >> 8) & 0xFF);
            return result;
        }
    }
}
