using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Accounts.Bip32;
using Nethereum.BlockchainProcessing.ProgressRepositories;
using Nethereum.DevChain;
using Nethereum.Documentation;
using Nethereum.PrivacyPools;
using Nethereum.PrivacyPools.Processing;
using Nethereum.PrivacyPools.Relayer;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.ZkProofs.RapidSnark;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.PrivacyPools.Circuits.Tests
{
    public class PrivacyPoolIntegrationTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;
        private const string TEST_MNEMONIC = "test test test test test test test test test test test junk";
        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const int CHAIN_ID = 31337;

        private DevChainNode _node;
        private IWeb3 _web3;
        private Account _account;
        private PrivacyPoolDeploymentResult _deployment;

        public PrivacyPoolIntegrationTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            _account = new Account(OWNER_PRIVATE_KEY, CHAIN_ID);
            var config = new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            _node = new DevChainNode(config);
            await _node.StartAsync(new[] { _account.Address }, Web3.Web3.Convert.ToWei(10000));
            _web3 = _node.CreateWeb3(_account);
            _account.TransactionManager.UseLegacyAsDefault = true;

            _deployment = await PrivacyPoolDeployer.DeployFullStackAsync(_web3,
                new PrivacyPoolDeploymentConfig { OwnerAddress = _account.Address });

            _output.WriteLine($"Entrypoint: {_deployment.Entrypoint.ContractAddress}");
            _output.WriteLine($"Pool: {_deployment.Pool.ContractAddress}");
        }

        public Task DisposeAsync() => Task.CompletedTask;

        private async Task ProcessEventsToCurrentBlockAsync(
            InMemoryPrivacyPoolRepository repository,
            PoseidonMerkleTree stateTree = null,
            InMemoryBlockchainProgressRepository progressRepo = null)
        {
            var currentBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var processingService = new PrivacyPoolLogProcessingService(_web3, _deployment.Pool.ContractAddress);
            var processor = processingService.CreateProcessor(repository, stateTree, progressRepo);
            await processor.ExecuteAsync(currentBlock, startAtBlockNumberIfNotProcessed: 0);
        }

        [Fact]
        [Trait("Category", "E2E-Integration")]
        [NethereumDocExample(DocSection.Protocols, "full-journey", "Full journey: deposit, process, recover, ragequit")]
        public async Task FullJourney_Deposit_Process_Recover_Ragequit()
        {
            var pp = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp.InitializeAsync();
            _output.WriteLine($"Scope: {pp.Scope}");

            var depositValue = Web3.Web3.Convert.ToWei(1);
            var depositResult = await pp.DepositAsync(depositValue, depositIndex: 0);
            Assert.False(depositResult.Receipt.HasErrors());
            _output.WriteLine($"Deposited: commitment={depositResult.Commitment.CommitmentHash}");

            var repository = new InMemoryPrivacyPoolRepository();
            var stateTree = new PoseidonMerkleTree();
            await ProcessEventsToCurrentBlockAsync(repository, stateTree);

            var deposits = await repository.GetDepositsAsync();
            var leaves = await repository.GetLeavesAsync();
            _output.WriteLine($"Processed: {deposits.Count} deposits, {leaves.Count} leaves");
            Assert.Single(deposits);
            Assert.Single(leaves);

            var onChainRoot = await pp.Pool.GetOnChainRootAsync();
            Assert.Equal(onChainRoot, stateTree.RootAsBigInteger);
            _output.WriteLine($"Tree root verified: {onChainRoot}");

            var withdrawals = await repository.GetWithdrawalsAsync();
            var ragequits = await repository.GetRagequitsAsync();
            var recovered = pp.RecoverAccounts(deposits, withdrawals, ragequits, leaves);

            Assert.Single(recovered);
            Assert.Equal(depositValue, recovered[0].SpendableValue);
            Assert.True(recovered[0].IsSpendable);
            Assert.Equal(depositResult.Commitment.CommitmentHash, recovered[0].Deposit.Commitment.CommitmentHash);
            _output.WriteLine($"Recovered: value={recovered[0].SpendableValue}, leaf={recovered[0].Deposit.LeafIndex}");

            var pp2 = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp2.InitializeAsync();
            var recovered2 = pp2.RecoverAccounts(deposits, withdrawals, ragequits, leaves);
            Assert.Single(recovered2);
            Assert.Equal(depositResult.Commitment.CommitmentHash, recovered2[0].Deposit.Commitment.CommitmentHash);
            _output.WriteLine("Recovery from fresh mnemonic: verified");

            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("commitment"))
            {
                _output.WriteLine("SKIP: Circuit artifacts not found");
                return;
            }

            var artifactSource = circuitSource;
            var proofProvider = pp.CreateProofProvider(new NativeProofProvider(), artifactSource);

            var spendable = pp.GetSpendableAccounts();
            Assert.Single(spendable);

            var ragequitResult = await pp.RagequitAsync(spendable[0], proofProvider);
            Assert.False(ragequitResult.Receipt.HasErrors());
            _output.WriteLine($"Ragequit tx: {ragequitResult.Receipt.TransactionHash}");

            var pool = new PrivacyPoolSimple.PrivacyPoolSimpleService(_web3, _deployment.Pool.ContractAddress);
            var isSpent = await pool.NullifierHashesQueryAsync(depositResult.Commitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("Nullifier spent: verified");
        }

        [Fact]
        [Trait("Category", "E2E-Integration")]
        [NethereumDocExample(DocSection.Protocols, "event-processing", "Process events and recover multiple accounts")]
        public async Task MultipleDeposits_ProcessAndRecover()
        {
            var pp = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp.InitializeAsync();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            for (int i = 0; i < 3; i++)
            {
                var result = await pp.DepositAsync(depositValue, depositIndex: i);
                Assert.False(result.Receipt.HasErrors());
                _output.WriteLine($"Deposit {i}: commitment={result.Commitment.CommitmentHash}");
            }

            var repository = new InMemoryPrivacyPoolRepository();
            var stateTree = new PoseidonMerkleTree();
            await ProcessEventsToCurrentBlockAsync(repository, stateTree);

            var deposits = await repository.GetDepositsAsync();
            var leaves = await repository.GetLeavesAsync();
            Assert.Equal(3, deposits.Count);
            Assert.Equal(3, leaves.Count);
            Assert.Equal(3, stateTree.Size);

            var onChainRoot = await pp.Pool.GetOnChainRootAsync();
            Assert.Equal(onChainRoot, stateTree.RootAsBigInteger);

            var withdrawals = await repository.GetWithdrawalsAsync();
            var ragequits = await repository.GetRagequitsAsync();
            var recovered = pp.RecoverAccounts(deposits, withdrawals, ragequits, leaves);
            Assert.Equal(3, recovered.Count);

            var spendable = pp.GetSpendableAccounts();
            Assert.Equal(3, spendable.Count);

            foreach (var account in spendable)
            {
                Assert.Equal(depositValue, account.SpendableValue);
                Assert.True(account.Deposit.LeafIndex >= 0);
                _output.WriteLine($"Account: leaf={account.Deposit.LeafIndex}, value={account.SpendableValue}");
            }
        }

        [Fact]
        [Trait("Category", "E2E-Integration")]
        [NethereumDocExample(DocSection.Protocols, "relayer", "Initialize relayer and report details")]
        public async Task Relayer_InitializesAndReportsDetails()
        {
            var pp = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp.InitializeAsync();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            await pp.DepositAsync(depositValue, depositIndex: 0);

            var repository = new InMemoryPrivacyPoolRepository();
            await ProcessEventsToCurrentBlockAsync(repository);

            var deposits = await repository.GetDepositsAsync();
            var withdrawals = await repository.GetWithdrawalsAsync();
            var ragequits = await repository.GetRagequitsAsync();
            var leaves = await repository.GetLeavesAsync();
            var recovered = pp.RecoverAccounts(deposits, withdrawals, ragequits, leaves);
            Assert.Single(pp.GetSpendableAccounts());

            var vkPath = System.IO.Path.Combine("circuits", "withdrawal", "withdrawal_vk.json");
            if (!System.IO.File.Exists(vkPath))
            {
                _output.WriteLine("SKIP: Verification key not found");
                return;
            }

            var withdrawalVk = System.IO.File.ReadAllText(vkPath);
            var verifier = new PrivacyPoolProofVerifier(withdrawalVk);

            var relayer = new PrivacyPoolRelayer(_web3, new RelayerConfig
            {
                EntrypointAddress = _deployment.Entrypoint.ContractAddress,
                PoolAddress = _deployment.Pool.ContractAddress,
                FeeReceiverAddress = _account.Address
            }, verifier);
            await relayer.InitializeAsync();

            var details = relayer.GetDetails();
            Assert.Equal(_deployment.Pool.ContractAddress, details.PoolAddress);
            _output.WriteLine($"Relayer: pool={details.PoolAddress}, scope={relayer.Scope}");
        }

        [Fact]
        [Trait("Category", "E2E-Integration")]
        [NethereumDocExample(DocSection.Protocols, "tree-caching", "Export, import, and incrementally update tree")]
        public async Task TreeExportImport_IncrementalUpdate()
        {
            var pp = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp.InitializeAsync();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            await pp.DepositAsync(depositValue, depositIndex: 0);
            await pp.DepositAsync(depositValue, depositIndex: 1);

            var repository = new InMemoryPrivacyPoolRepository();
            var tree1 = new PoseidonMerkleTree();
            var progressRepo = new InMemoryBlockchainProgressRepository();
            await ProcessEventsToCurrentBlockAsync(repository, tree1, progressRepo);

            Assert.Equal(2, tree1.Size);
            var root1 = tree1.RootAsBigInteger;
            var lastBlock = await progressRepo.GetLastBlockNumberProcessedAsync();
            _output.WriteLine($"Tree after 2 deposits: root={root1}, lastBlock={lastBlock}");

            var exported = tree1.Export();
            _output.WriteLine($"Exported: {exported.Length} chars");

            await pp.DepositAsync(depositValue, depositIndex: 2);
            await pp.DepositAsync(depositValue, depositIndex: 3);

            var tree2 = PoseidonMerkleTree.Import(exported);
            Assert.Equal(2, tree2.Size);
            Assert.Equal(root1, tree2.RootAsBigInteger);

            var repository2 = new InMemoryPrivacyPoolRepository();
            var currentBlock = await _web3.Eth.Blocks.GetBlockNumber.SendRequestAsync();
            var processingService2 = new PrivacyPoolLogProcessingService(_web3, _deployment.Pool.ContractAddress);
            var processor2 = processingService2.CreateProcessor(repository2, tree2);
            await processor2.ExecuteAsync(currentBlock,
                startAtBlockNumberIfNotProcessed: (lastBlock.Value + 1));

            Assert.Equal(4, tree2.Size);
            var onChainRoot = await pp.Pool.GetOnChainRootAsync();
            Assert.Equal(onChainRoot, tree2.RootAsBigInteger);
            _output.WriteLine($"After incremental: root={tree2.RootAsBigInteger}, size={tree2.Size}");
        }
    }
}
