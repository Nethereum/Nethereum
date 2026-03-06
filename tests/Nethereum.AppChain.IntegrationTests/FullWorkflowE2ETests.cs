using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.CoreChain.RocksDB;
using Nethereum.CoreChain.RocksDB.Stores;
using Nethereum.CoreChain.Storage;
using Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor;
using Nethereum.AppChain.Anchoring.Contracts.AppChainAnchor.AppChainAnchor.ContractDefinition;
using Nethereum.AppChain.Genesis;
using Nethereum.AppChain.Policy.Bootstrap;
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy;
using Nethereum.AppChain.Policy.Contracts.AppChainPolicy.AppChainPolicy.ContractDefinition;
using Nethereum.AppChain.Sequencer;
using Nethereum.DevChain;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Xunit;

using Account = Nethereum.Web3.Accounts.Account;
using AppChainCore = Nethereum.AppChain.AppChain;

namespace Nethereum.AppChain.IntegrationTests
{
    [Collection("Sequential")]
    public class FullWorkflowE2ETests : IAsyncLifetime, IDisposable
    {
        private DevChainNode? _l1Node;
        private Web3.Web3? _l1Web3;
        private AppChainAnchorService? _anchorService;
        private AppChainPolicyService? _policyService;

        private string _databasePath = "";
        private RocksDbManager? _dbManager;
        private AppChainCore? _appChain;
        private Sequencer.Sequencer? _sequencer;
        private PolicyMigrationService _migrationService = new PolicyMigrationService();

        private const string SequencerPrivateKey = "0x8da4ef21b864d2cc526dbdb2a120bd2874c36c9d0a1fb7f8c63d7f7a8b41de8f";
        private readonly string _sequencerAddress;
        private const string UserPrivateKey = "0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318";
        private readonly string _userAddress;
        private static readonly BigInteger AppChainId = new BigInteger(420420);

        public FullWorkflowE2ETests()
        {
            var sequencerKey = new EthECKey(SequencerPrivateKey);
            _sequencerAddress = sequencerKey.GetPublicAddress();

            var userKey = new EthECKey(UserPrivateKey);
            _userAddress = userKey.GetPublicAddress();
        }

        public async Task InitializeAsync()
        {
            var l1Config = new DevChainConfig
            {
                ChainId = 1337,
                BlockGasLimit = 30_000_000,
                BaseFee = 0,
                InitialBalance = BigInteger.Parse("100000000000000000000000")
            };
            _l1Node = new DevChainNode(l1Config);
            await _l1Node.StartAsync(new[] { _sequencerAddress, _userAddress });

            var l1Account = new Account(SequencerPrivateKey, 1337);
            var l1RpcClient = new DevChainRpcClient(_l1Node, 1337);
            _l1Web3 = new Web3.Web3(l1Account, l1RpcClient);
            _l1Web3.TransactionManager.UseLegacyAsDefault = true;

            var bootstrapConfig = new BootstrapPolicyConfig
            {
                AllowedWriters = new List<string> { _sequencerAddress, _userAddress },
                AllowedAdmins = new List<string> { _sequencerAddress }
            };
            var migrationData = _migrationService.PrepareMigrationData(bootstrapConfig);

            var anchorDeployment = new AppChainAnchorDeployment
            {
                AppChainId = AppChainId,
                Sequencer = _sequencerAddress
            };
            var anchorReceipt = await AppChainAnchorService.DeployContractAndWaitForReceiptAsync(_l1Web3, anchorDeployment);
            _anchorService = new AppChainAnchorService(_l1Web3, anchorReceipt.ContractAddress!);

            var policyDeployment = new AppChainPolicyDeployment
            {
                AppChainId = AppChainId,
                Sequencer = _sequencerAddress,
                InitialWritersRoot = migrationData.WritersRoot,
                InitialAdminsRoot = migrationData.AdminsRoot
            };
            var policyReceipt = await AppChainPolicyService.DeployContractAndWaitForReceiptAsync(_l1Web3, policyDeployment);
            _policyService = new AppChainPolicyService(_l1Web3, policyReceipt.ContractAddress!);

            _databasePath = Path.Combine(Path.GetTempPath(), $"appchain_workflow_{Guid.NewGuid():N}");
            var options = new RocksDbStorageOptions { DatabasePath = _databasePath };
            _dbManager = new RocksDbManager(options);

            var blockStore = new RocksDbBlockStore(_dbManager);
            var transactionStore = new RocksDbTransactionStore(_dbManager, blockStore);
            var receiptStore = new RocksDbReceiptStore(_dbManager, blockStore);
            var logStore = new RocksDbLogStore(_dbManager);
            var stateStore = new RocksDbStateStore(_dbManager);

            var appChainConfig = AppChainConfig.CreateWithName("TestAppChain", (int)AppChainId);
            appChainConfig.SequencerAddress = _sequencerAddress;

            _appChain = new AppChainCore(appChainConfig, blockStore, transactionStore, receiptStore, logStore, stateStore);

            var genesisOptions = new GenesisOptions
            {
                PrefundedAddresses = new[] { _sequencerAddress, _userAddress },
                PrefundBalance = BigInteger.Parse("10000000000000000000000"),
                DeployCreate2Factory = true
            };
            await _appChain.InitializeAsync(genesisOptions);

            var sequencerConfig = new SequencerConfig
            {
                SequencerAddress = _sequencerAddress,
                SequencerPrivateKey = SequencerPrivateKey,
                BlockTimeMs = 0,
                MaxTransactionsPerBlock = 100,
                Policy = Nethereum.AppChain.Sequencer.PolicyConfig.RestrictedAccess(new List<string> { _sequencerAddress, _userAddress })
            };
            _sequencer = new Sequencer.Sequencer(_appChain, sequencerConfig);
            await _sequencer.StartAsync();
        }

        public Task DisposeAsync()
        {
            Dispose();
            return Task.CompletedTask;
        }

        public void Dispose()
        {
            _sequencer = null;
            _appChain = null;
            _dbManager?.Dispose();
            _l1Node?.Dispose();
            _l1Node = null;

            if (!string.IsNullOrEmpty(_databasePath) && Directory.Exists(_databasePath))
            {
                try { Directory.Delete(_databasePath, true); } catch { }
            }
        }

        [Fact]
        public async Task Workflow_ContractsDeployedToL1()
        {
            var anchorCode = await _l1Web3!.Eth.GetCode.SendRequestAsync(_anchorService!.ContractHandler.ContractAddress);
            var policyCode = await _l1Web3.Eth.GetCode.SendRequestAsync(_policyService!.ContractHandler.ContractAddress);

            Assert.NotEmpty(anchorCode);
            Assert.NotEmpty(policyCode);
        }

        [Fact]
        public async Task Workflow_AppChainInitialized()
        {
            var blockNumber = await _appChain!.GetBlockNumberAsync();
            var create2Code = await _appChain.GetCodeAsync(Create2FactoryGenesisBuilder.CREATE2_FACTORY_ADDRESS);

            Assert.Equal(0, blockNumber);
            Assert.NotNull(create2Code);
            Assert.True(create2Code.Length > 0);
        }

        [Fact]
        public async Task Workflow_SequencerProducesBlocks()
        {
            await _sequencer!.ProduceBlockAsync();
            await _sequencer.ProduceBlockAsync();
            await _sequencer.ProduceBlockAsync();

            var blockNumber = await _sequencer.GetBlockNumberAsync();
            Assert.Equal(3, blockNumber);
        }

        [Fact]
        public async Task Workflow_AnchorToL1()
        {
            await _sequencer!.ProduceBlockAsync();
            var latestBlock = await _sequencer.GetLatestBlockAsync();

            Assert.NotNull(latestBlock);

            var stateRoot = latestBlock.StateRoot ?? new byte[32];
            var txRoot = latestBlock.TransactionsHash ?? new byte[32];
            var receiptRoot = latestBlock.ReceiptHash ?? new byte[32];

            var receipt = await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                (BigInteger)latestBlock.BlockNumber,
                stateRoot,
                txRoot,
                receiptRoot);

            Assert.NotNull(receipt);
            Assert.False(receipt.Failed());

            var events = receipt.DecodeAllEvents<AnchoredEventDTO>();
            Assert.Single(events);

            var anchor = await _anchorService.GetAnchorQueryAsync((BigInteger)latestBlock.BlockNumber);
            Assert.Equal(stateRoot, anchor.StateRoot);
        }

        [Fact]
        public async Task Workflow_VerifyAnchor()
        {
            await _sequencer!.ProduceBlockAsync();
            var latestBlock = await _sequencer.GetLatestBlockAsync();

            var stateRoot = latestBlock!.StateRoot ?? new byte[32];
            var txRoot = latestBlock.TransactionsHash ?? new byte[32];
            var receiptRoot = latestBlock.ReceiptHash ?? new byte[32];

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                (BigInteger)latestBlock.BlockNumber,
                stateRoot,
                txRoot,
                receiptRoot);

            var isValid = await _anchorService.VerifyAnchorQueryAsync(
                (BigInteger)latestBlock.BlockNumber,
                stateRoot,
                txRoot,
                receiptRoot);

            Assert.True(isValid);

            var wrongRoot = new byte[32];
            wrongRoot[0] = 0xFF;
            var isInvalid = await _anchorService.VerifyAnchorQueryAsync(
                (BigInteger)latestBlock.BlockNumber,
                wrongRoot,
                txRoot,
                receiptRoot);

            Assert.False(isInvalid);
        }

        [Fact]
        public async Task Workflow_MultiBlockAnchorCadence()
        {
            for (int i = 0; i < 5; i++)
            {
                await _sequencer!.ProduceBlockAsync();
            }

            var block3 = await _appChain!.GetBlockByNumberAsync(new BigInteger(3));
            Assert.NotNull(block3);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                new BigInteger(3),
                block3.StateRoot ?? new byte[32],
                block3.TransactionsHash ?? new byte[32],
                block3.ReceiptHash ?? new byte[32]);

            await _anchorService.AnchorRequestAndWaitForReceiptAsync(
                new BigInteger(5),
                (await _appChain.GetBlockByNumberAsync(new BigInteger(5)))!.StateRoot ?? new byte[32],
                (await _appChain.GetBlockByNumberAsync(new BigInteger(5)))!.TransactionsHash ?? new byte[32],
                (await _appChain.GetBlockByNumberAsync(new BigInteger(5)))!.ReceiptHash ?? new byte[32]);

            var latestAnchoredBlock = await _anchorService.LatestBlockQueryAsync();
            Assert.Equal(5, latestAnchoredBlock);
        }

        [Fact]
        public async Task Workflow_PolicyEnforcesWriters()
        {
            var writersProof = _migrationService.ComputeMerkleProof(
                _sequencerAddress, new[] { _sequencerAddress, _userAddress });
            var writersRoot = await _policyService!.WritersRootQueryAsync();

            var isSequencerValid = await _policyService.IsValidWriterQueryAsync(
                _sequencerAddress,
                writersProof.ToList(),
                new List<byte[]>());

            var isUserValid = await _policyService.IsValidWriterQueryAsync(
                _userAddress,
                _migrationService.ComputeMerkleProof(_userAddress, new[] { _sequencerAddress, _userAddress }).ToList(),
                new List<byte[]>());

            var unknownAddress = "0x0000000000000000000000000000000000000001";
            var isUnknownValid = await _policyService.IsValidWriterQueryAsync(
                unknownAddress,
                new List<byte[]>(),
                new List<byte[]>());

            Assert.True(isSequencerValid);
            Assert.True(isUserValid);
            Assert.False(isUnknownValid);
        }

        [Fact]
        public async Task Workflow_FullCycle_BlockProduction_Anchoring()
        {
            await _sequencer!.ProduceBlockAsync();
            var block1 = await _sequencer.GetLatestBlockAsync();

            await _sequencer.ProduceBlockAsync();
            var block2 = await _sequencer.GetLatestBlockAsync();

            Assert.Equal(1, block1!.BlockNumber);
            Assert.Equal(2, block2!.BlockNumber);

            await _anchorService!.AnchorRequestAndWaitForReceiptAsync(
                (BigInteger)block2.BlockNumber,
                block2.StateRoot ?? new byte[32],
                block2.TransactionsHash ?? new byte[32],
                block2.ReceiptHash ?? new byte[32]);

            var anchor = await _anchorService.GetAnchorQueryAsync((BigInteger)block2.BlockNumber);
            Assert.Equal(block2.StateRoot ?? new byte[32], anchor.StateRoot);

            var isValid = await _anchorService.VerifyAnchorQueryAsync(
                (BigInteger)block2.BlockNumber,
                block2.StateRoot ?? new byte[32],
                block2.TransactionsHash ?? new byte[32],
                block2.ReceiptHash ?? new byte[32]);
            Assert.True(isValid);
        }
    }
}
