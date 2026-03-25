using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.PrivacyPools;
using Nethereum.PrivacyPools.Entrypoint;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Nethereum.PrivacyPools.ERC1967Proxy;
using Nethereum.PrivacyPools.ERC1967Proxy.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Nethereum.PrivacyPools.PrivacyPoolSimple;
using Nethereum.PrivacyPools.PrivacyPoolSimple.ContractDefinition;
using Nethereum.PrivacyPools.WithdrawalVerifier;
using Nethereum.PrivacyPools.WithdrawalVerifier.ContractDefinition;
using Nethereum.PrivacyPools.CommitmentVerifier;
using Nethereum.PrivacyPools.CommitmentVerifier.ContractDefinition;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.ZkProofs;
using Nethereum.ZkProofs.Groth16;
using Nethereum.ZkProofs.RapidSnark;
using Xunit;
using Xunit.Abstractions;
using DepositFunction = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.DepositFunction;
using EntrypointWithdrawal = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Withdrawal;
using EntrypointWithdrawProof = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.WithdrawProof;
using PoolDepositedEvent = Nethereum.PrivacyPools.PrivacyPoolBase.DepositedEventDTO;
using PoolWithdrawnEvent = Nethereum.PrivacyPools.PrivacyPoolBase.WithdrawnEventDTO;
using PoolRagequitEvent = Nethereum.PrivacyPools.PrivacyPoolBase.RagequitEventDTO;
using EntrypointDepositedEvent = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.DepositedEventDTO;
using EntrypointPoolRegisteredEvent = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.PoolRegisteredEventDTO;
using Nethereum.PrivacyPools.PoseidonT3;
using Nethereum.PrivacyPools.PoseidonT3.ContractDefinition;
using Nethereum.PrivacyPools.PoseidonT4;
using Nethereum.PrivacyPools.PoseidonT4.ContractDefinition;

namespace Nethereum.PrivacyPools.Circuits.Tests
{
    public class PrivacyPoolsE2ETests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;

        private const int DEVCHAIN_CHAIN_ID = 31337;
        private const int GETH_DEV_CHAIN_ID = 1337;
        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string NATIVE_ASSET = "0xEeeeeEeeeEeEeeEeEeEeeEEEeeeeEeeeeeeeEEeE";
        private const string GETH_RPC_URL = "http://127.0.0.1:18545";
        private static readonly string GETH_PATH = FindGethExe();

        private DevChainNode? _node;
        private Process? _gethProcess;
        private string? _gethDataDir;
        private IWeb3 _web3 = null!;
        private Account _account = null!;
        private bool _useGeth;

        public PrivacyPoolsE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

        private static string FindGethExe()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var candidate = Path.Combine(dir, "testchain", "clique", "geth.exe");
                if (File.Exists(candidate)) return candidate;
                dir = Path.GetDirectoryName(dir);
            }
            return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "testchain", "clique", "geth.exe");
        }

        private static bool ShouldUseGeth()
        {
            var envVar = Environment.GetEnvironmentVariable("PRIVACY_POOLS_USE_GETH");
            return string.Equals(envVar, "true", StringComparison.OrdinalIgnoreCase)
                || string.Equals(envVar, "1", StringComparison.OrdinalIgnoreCase);
        }

        public async Task InitializeAsync()
        {
            _useGeth = ShouldUseGeth();

            if (_useGeth)
            {
                await StartGethDevAsync();
            }
            else
            {
                await StartDevChainAsync();
            }
        }

        private async Task StartDevChainAsync()
        {
            _account = new Account(OWNER_PRIVATE_KEY, DEVCHAIN_CHAIN_ID);

            var config = new DevChainConfig
            {
                ChainId = DEVCHAIN_CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            _node = new DevChainNode(config);
            await _node.StartAsync(new[] { _account.Address }, Web3.Web3.Convert.ToWei(10000));

            _web3 = _node.CreateWeb3(_account);
            _account.TransactionManager.UseLegacyAsDefault = true;

            _output.WriteLine($"DevChain started, account: {_account.Address}");
        }

        private async Task StartGethDevAsync()
        {
            var gethExe = GETH_PATH;
            if (!File.Exists(gethExe))
                throw new FileNotFoundException($"geth.exe not found at {gethExe}");

            _gethDataDir = Path.Combine(Path.GetTempPath(), $"geth-privacypools-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_gethDataDir);

            _gethProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gethExe,
                    Arguments = $"--dev --dev.period 1 --dev.gaslimit 30000000 " +
                                $"--datadir \"{_gethDataDir}\" " +
                                $"--http --http.addr 127.0.0.1 --http.port 18545 " +
                                $"--http.api eth,net,web3,debug,personal,admin " +
                                $"--verbosity 2",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true
                }
            };

            _gethProcess.Start();
            _output.WriteLine($"Geth dev started (PID {_gethProcess.Id}), datadir: {_gethDataDir}");

            _account = new Account(OWNER_PRIVATE_KEY, GETH_DEV_CHAIN_ID);
            _account.TransactionManager.UseLegacyAsDefault = true;

            var web3 = new Web3.Web3(_account, GETH_RPC_URL);
            _web3 = web3;

            await WaitForGethReadyAsync(web3);

            var devAccountBalance = await _web3.Eth.GetBalance.SendRequestAsync(_account.Address);
            _output.WriteLine($"Geth dev account {_account.Address} balance: {Web3.Web3.Convert.FromWei(devAccountBalance.Value)} ETH");

            if (devAccountBalance.Value == 0)
            {
                _output.WriteLine("Funding account from geth dev coinbase...");
                await FundAccountFromCoinbaseAsync();
            }
        }

        private async Task WaitForGethReadyAsync(Web3.Web3 web3, int timeoutSeconds = 30)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                try
                {
                    var chainId = await web3.Eth.ChainId.SendRequestAsync();
                    _output.WriteLine($"Geth ready, chainId: {chainId.Value}");
                    return;
                }
                catch
                {
                    await Task.Delay(500);
                }
            }
            throw new TimeoutException($"Geth did not become ready within {timeoutSeconds}s");
        }

        private async Task FundAccountFromCoinbaseAsync()
        {
            var adminWeb3 = new Web3.Web3(GETH_RPC_URL);
            var accounts = await adminWeb3.Eth.Accounts.SendRequestAsync();
            if (accounts == null || accounts.Length == 0)
                throw new Exception("No geth dev accounts found");

            var coinbase = accounts[0];
            _output.WriteLine($"Coinbase: {coinbase}");

            var fundAmount = Web3.Web3.Convert.ToWei(10000);
            var txHash = await adminWeb3.Eth.TransactionManager.SendTransactionAsync(
                coinbase, _account.Address, new Nethereum.Hex.HexTypes.HexBigInteger(fundAmount));

            var receipt = await adminWeb3.TransactionReceiptPolling.PollForReceiptAsync(txHash);
            _output.WriteLine($"Funded {_account.Address} with 10000 ETH, tx: {txHash}");
        }

        public async Task DisposeAsync()
        {
            if (_gethProcess != null)
            {
                try
                {
                    _gethProcess.Kill(entireProcessTree: true);
                    await _gethProcess.WaitForExitAsync();
                }
                catch { }
                _gethProcess.Dispose();
                _gethProcess = null;
            }

            if (_gethDataDir != null && Directory.Exists(_gethDataDir))
            {
                try { Directory.Delete(_gethDataDir, recursive: true); } catch { }
            }
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Deploy_Simple_Verifier()
        {
            var verifierService = await WithdrawalVerifierService.DeployContractAndGetServiceAsync(
                _web3, new WithdrawalVerifierDeployment());
            Assert.NotNull(verifierService.ContractAddress);
            _output.WriteLine($"  WithdrawalVerifier: {verifierService.ContractAddress}");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Deploy_Entrypoint_Via_UUPS_Proxy()
        {
            var account = _account.Address;

            var implReceipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                EntrypointDeploymentBase.BYTECODE,
                _account.Address,
                new Nethereum.Hex.HexTypes.HexBigInteger(10_000_000));
            Assert.NotNull(implReceipt.ContractAddress);
            _output.WriteLine($"  Entrypoint impl: {implReceipt.ContractAddress}");

            var initFunction = new InitializeFunction { Owner = account, Postman = account };
            var initCalldata = initFunction.GetCallData();

            var proxyDeployment = new ERC1967ProxyDeployment
            {
                Implementation = implReceipt.ContractAddress,
                Data = initCalldata,
                Gas = (BigInteger)5_000_000
            };
            var proxyReceipt = await ERC1967ProxyService.DeployContractAndWaitForReceiptAsync(
                _web3, proxyDeployment);
            Assert.NotNull(proxyReceipt.ContractAddress);
            _output.WriteLine($"  Proxy: {proxyReceipt.ContractAddress}");

            var entrypointService = new EntrypointService(_web3, proxyReceipt.ContractAddress);

            var ownerRole = "0xb19546dff01e856fb3f010c267a7b1c60363cf8a4664e21cc89c26224620214e".HexToByteArray();
            var hasRole = await entrypointService.HasRoleQueryAsync(ownerRole, account);
            Assert.True(hasRole);
            _output.WriteLine("  Owner role verified");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Deploy_Pool_And_Register()
        {
            var (entrypointService, _) = await DeployEntrypointViaProxyAsync();

            var withdrawalVerifierService = await WithdrawalVerifierService.DeployContractAndGetServiceAsync(
                _web3, new WithdrawalVerifierDeployment());
            var commitmentVerifierService = await CommitmentVerifierService.DeployContractAndGetServiceAsync(
                _web3, new CommitmentVerifierDeployment());

            var poseidonT3Service = await PoseidonT3Service.DeployContractAndGetServiceAsync(
                _web3, new PoseidonT3Deployment { Gas = (BigInteger)15_000_000 });
            var poseidonT4Service = await PoseidonT4Service.DeployContractAndGetServiceAsync(
                _web3, new PoseidonT4Deployment { Gas = (BigInteger)10_000_000 });

            var poolDeployment = new PrivacyPoolSimpleDeployment
            {
                Entrypoint = entrypointService.ContractAddress,
                WithdrawalVerifier = withdrawalVerifierService.ContractAddress,
                RagequitVerifier = commitmentVerifierService.ContractAddress,
                Gas = (BigInteger)10_000_000
            };
            poolDeployment.LinkLibraries(poseidonT3Service.ContractAddress, poseidonT4Service.ContractAddress);

            var poolService = await PrivacyPoolSimpleService.DeployContractAndGetServiceAsync(
                _web3, poolDeployment);
            _output.WriteLine($"  Pool: {poolService.ContractAddress}");

            var scope = await poolService.ScopeQueryAsync();
            Assert.NotEqual(BigInteger.Zero, scope);
            _output.WriteLine($"  Scope: {scope}");

            var dead = await poolService.DeadQueryAsync();
            Assert.False(dead);

            var treeSize = await poolService.CurrentTreeSizeQueryAsync();
            Assert.Equal(BigInteger.Zero, treeSize);

            var registerReceipt = await entrypointService.RegisterPoolRequestAndWaitForReceiptAsync(
                NATIVE_ASSET, poolService.ContractAddress,
                BigInteger.Zero, BigInteger.Zero, BigInteger.Zero);
            Assert.False(registerReceipt.HasErrors());

            var registeredEvents = registerReceipt.DecodeAllEvents<EntrypointPoolRegisteredEvent>();
            Assert.Single(registeredEvents);
            _output.WriteLine("  Pool registered");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Deposit_Native_Via_Entrypoint()
        {
            var (entrypointService, poolService) = await DeployFullStackAsync();

            var (nullifier, secret, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            var depositFunction = new DepositFunction
            {
                Precommitment = precommitment,
                AmountToSend = depositValue
            };
            var depositReceipt = await entrypointService.DepositRequestAndWaitForReceiptAsync(depositFunction);
            Assert.False(depositReceipt.HasErrors());

            var entrypointDepositEvents = depositReceipt.DecodeAllEvents<EntrypointDepositedEvent>();
            Assert.Single(entrypointDepositEvents);

            var poolDepositEvents = depositReceipt.DecodeAllEvents<PoolDepositedEvent>();
            Assert.Single(poolDepositEvents);
            var poolEvent = poolDepositEvents.First().Event;

            Assert.True(poolEvent.Commitment > BigInteger.Zero);
            Assert.True(poolEvent.Label > BigInteger.Zero);
            Assert.Equal(depositValue, poolEvent.Value);
            _output.WriteLine($"  Commitment: {poolEvent.Commitment}");
            _output.WriteLine($"  Label: {poolEvent.Label}");

            var commitment = PrivacyPoolCommitment.Create(
                poolEvent.Value, poolEvent.Label, nullifier, secret);
            Assert.Equal(poolEvent.Commitment, commitment.CommitmentHash);
            _output.WriteLine("  Commitment hash matches on-chain");

            var treeSize = await poolService.CurrentTreeSizeQueryAsync();
            Assert.Equal(BigInteger.One, treeSize);
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Deposit_Two_And_Verify_Merkle_Root()
        {
            var (entrypointService, poolService) = await DeployFullStackAsync();

            var tree = new PoseidonMerkleTree();

            for (int i = 0; i < 2; i++)
            {
                var (nullifier, secret, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();
                var depositValue = Web3.Web3.Convert.ToWei(1);

                var depositFunction = new DepositFunction
                {
                    Precommitment = precommitment,
                    AmountToSend = depositValue
                };
                var receipt = await entrypointService.DepositRequestAndWaitForReceiptAsync(depositFunction);
                Assert.False(receipt.HasErrors());

                var poolEvents = receipt.DecodeAllEvents<PoolDepositedEvent>();
                Assert.Single(poolEvents);

                var evt = poolEvents.First().Event;
                var commitment = PrivacyPoolCommitment.Create(evt.Value, evt.Label, nullifier, secret);
                Assert.Equal(evt.Commitment, commitment.CommitmentHash);

                tree.InsertCommitment(commitment.CommitmentHash);
                _output.WriteLine($"  Deposit {i}: commitment={evt.Commitment}");
            }

            Assert.Equal(2, tree.Size);

            var onChainRoot = await poolService.CurrentRootQueryAsync();
            Assert.Equal(onChainRoot, tree.RootAsBigInteger);
            _output.WriteLine($"  Roots match: {onChainRoot}");

            for (int i = 0; i < 2; i++)
            {
                var proof = tree.GenerateInclusionProof(i);
                Assert.NotNull(proof);
            }
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Ragequit_After_Deposit()
        {
            var (entrypointService, poolService) = await DeployFullStackAsync();

            var (nullifier, secret, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();
            var depositValue = Web3.Web3.Convert.ToWei(1);

            var depositFunction = new DepositFunction
            {
                Precommitment = precommitment,
                AmountToSend = depositValue
            };
            var depositReceipt = await entrypointService.DepositRequestAndWaitForReceiptAsync(depositFunction);
            Assert.False(depositReceipt.HasErrors());

            var poolDepositEvents = depositReceipt.DecodeAllEvents<PoolDepositedEvent>();
            var poolEvent = poolDepositEvents.First().Event;
            var commitment = PrivacyPoolCommitment.Create(
                poolEvent.Value, poolEvent.Label, nullifier, secret);
            Assert.Equal(poolEvent.Commitment, commitment.CommitmentHash);
            _output.WriteLine($"  Deposited: commitment={commitment.CommitmentHash}");

            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("commitment"))
            {
                Assert.Fail("Commitment circuit artifacts not found. Cannot run ragequit E2E.");
                return;
            }

            ICircuitArtifactSource artifactSource = circuitSource;

            var proofProvider = new PrivacyPoolProofProvider(
                new NativeProofProvider(), artifactSource);

            var ragequitResult = await proofProvider.GenerateRagequitProofAsync(new RagequitWitnessInput
            {
                Nullifier = commitment.Nullifier,
                Secret = commitment.Secret,
                Value = commitment.Value,
                Label = commitment.Label
            });

            Assert.Equal(commitment.NullifierHash, ragequitResult.Signals.NullifierHash);
            Assert.Equal(commitment.CommitmentHash, ragequitResult.Signals.CommitmentHash);
            _output.WriteLine("  ZK proof generated");

            var ragequitProof = PrivacyPoolProofConverter.ToRagequitProof(
                ragequitResult.ProofJson, ragequitResult.Signals);

            var treeSize = await poolService.CurrentTreeSizeQueryAsync();
            _output.WriteLine($"  Tree size: {treeSize}");

            var verifierAddress = await poolService.RagequitVerifierQueryAsync();
            var verifierService = new CommitmentVerifierService(_web3, verifierAddress);
            var verifyResult = await verifierService.VerifyProofQueryAsync(
                ragequitProof.PA, ragequitProof.PB, ragequitProof.PC, ragequitProof.PubSignals);
            _output.WriteLine($"  Direct verifier result: {verifyResult}");
            Assert.True(verifyResult, "Groth16 proof verification failed on-chain");

            var ragequitFunction = new RagequitFunction { Proof = ragequitProof, Gas = (BigInteger)5_000_000 };
            var ragequitReceipt = await poolService.RagequitRequestAndWaitForReceiptAsync(ragequitFunction);
            Assert.False(ragequitReceipt.HasErrors(), $"Ragequit tx failed. Status: {ragequitReceipt.Status?.Value}");
            _output.WriteLine("  Ragequit tx succeeded");

            var isSpent = await poolService.NullifierHashesQueryAsync(commitment.NullifierHash);
            Assert.True(isSpent);

            var ragequitEvents = ragequitReceipt.DecodeAllEvents<PoolRagequitEvent>();
            Assert.Single(ragequitEvents);
            _output.WriteLine("  Nullifier spent, event emitted");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task ASPTreeService_BuildPublishAndWithdraw()
        {
            var (entrypointService, poolService) = await DeployFullStackAsync();

            var pp = PrivacyPool.FromDeployment(_web3,
                new PrivacyPoolDeploymentResult
                {
                    Entrypoint = entrypointService,
                    Pool = poolService
                },
                "test test test test test test test test test test test junk");
            await pp.InitializeAsync();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            var dep1 = await pp.DepositAsync(depositValue, depositIndex: 0);
            var dep2 = await pp.DepositAsync(depositValue, depositIndex: 1);
            Assert.False(dep1.Receipt.HasErrors());
            Assert.False(dep2.Receipt.HasErrors());

            var label1 = dep1.Receipt.DecodeAllEvents<PoolDepositedEvent>().First().Event.Label;
            var label2 = dep2.Receipt.DecodeAllEvents<PoolDepositedEvent>().First().Event.Label;
            _output.WriteLine($"  Deposit labels: {label1}, {label2}");

            var asp = pp.CreateASPTreeService();
            asp.InsertLabel(label1);
            asp.InsertLabel(label2);
            Assert.Equal(2, asp.Size);
            _output.WriteLine($"  ASP root: {asp.Root}");

            await asp.PublishRootAsync("bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu");
            Assert.True(await asp.IsRootPublishedAsync());
            _output.WriteLine("  ASP root published and verified on-chain");

            var (siblings, index) = asp.GenerateProof(label1);
            Assert.Equal(PrivacyPoolConstants.MAX_TREE_DEPTH, siblings.Length);
            _output.WriteLine($"  Proof for label1: index={index}, siblings={siblings.Count(s => s != BigInteger.Zero)} non-zero");

            var exported = asp.Export();
            var asp2 = pp.CreateASPTreeService();
            asp2.Import(exported);
            Assert.Equal(asp.Root, asp2.Root);
            _output.WriteLine("  ASP tree export/import verified");

            var stateTree = new PoseidonMerkleTree();
            stateTree.InsertCommitment(dep1.Commitment.CommitmentHash);
            stateTree.InsertCommitment(dep2.Commitment.CommitmentHash);

            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("withdrawal"))
            {
                Assert.Fail("Withdrawal circuit artifacts not found");
                return;
            }

            var proofProvider = pp.CreateProofProvider(
                new NativeProofProvider(), circuitSource);

            var withdrawResult = await pp.Pool.WithdrawDirectAsync(
                dep1.Commitment, 0, depositValue / 2, _account.Address,
                proofProvider, stateTree, asp.Tree);

            Assert.False(withdrawResult.Receipt.HasErrors());
            var isSpent = await poolService.NullifierHashesQueryAsync(dep1.Commitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("  ASP-managed direct withdrawal: SUCCESS");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Full_Withdrawal_Cycle()
        {
            var account = _account.Address;
            var (entrypointService, poolService) = await DeployFullStackAsync();
            var scope = await poolService.ScopeQueryAsync();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            var (nullifier, secret, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();

            var depositFunction = new DepositFunction
            {
                Precommitment = precommitment,
                AmountToSend = depositValue
            };
            var depositReceipt = await entrypointService.DepositRequestAndWaitForReceiptAsync(depositFunction);
            Assert.False(depositReceipt.HasErrors());

            var poolDepositEvents = depositReceipt.DecodeAllEvents<PoolDepositedEvent>();
            var poolEvent = poolDepositEvents.First().Event;
            var commitment = PrivacyPoolCommitment.Create(
                poolEvent.Value, poolEvent.Label, nullifier, secret);
            Assert.Equal(poolEvent.Commitment, commitment.CommitmentHash);
            _output.WriteLine($"  Deposited: {depositValue} wei, commitment={commitment.CommitmentHash}");

            var stateTree = new PoseidonMerkleTree();
            stateTree.InsertCommitment(commitment.CommitmentHash);

            var onChainRoot = await poolService.CurrentRootQueryAsync();
            Assert.Equal(onChainRoot, stateTree.RootAsBigInteger);
            _output.WriteLine($"  State tree root matches: {onChainRoot}");

            var aspTree = new PoseidonMerkleTree();
            aspTree.InsertCommitment(commitment.Label);

            var aspRoot = aspTree.RootAsBigInteger;
            var ipfsCID = "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu";
            var updateRootReceipt = await entrypointService.UpdateRootRequestAndWaitForReceiptAsync(aspRoot, ipfsCID);
            Assert.False(updateRootReceipt.HasErrors());

            var latestRoot = await entrypointService.LatestRootQueryAsync();
            Assert.Equal(aspRoot, latestRoot);
            _output.WriteLine($"  ASP root updated: {aspRoot}");

            var withdrawnValue = depositValue / 2;
            var newCommitment = PrivacyPoolCommitment.CreateRandom(
                depositValue - withdrawnValue, poolEvent.Label);

            var stateMerkleProof = stateTree.GenerateInclusionProof(0);
            var stateSiblings = stateTree.GetProofSiblings(stateMerkleProof);
            var paddedStateSiblings = PadSiblings(stateSiblings, 32);

            var aspMerkleProof = aspTree.GenerateInclusionProof(0);
            var aspSiblings = aspTree.GetProofSiblings(aspMerkleProof);
            var paddedASPSiblings = PadSiblings(aspSiblings, 32);

            var relayData = new ABIEncode().GetABIEncoded(
                new ABIValue("address", account),
                new ABIValue("address", account),
                new ABIValue("uint256", BigInteger.Zero));

            var withdrawal = new EntrypointWithdrawal
            {
                Processooor = entrypointService.ContractAddress,
                Data = relayData
            };

            var contextParams = new WithdrawalContextParams
            {
                Withdrawal = withdrawal,
                Scope = scope
            };
            var contextInput = new ABIEncode().GetABIParamsEncoded(contextParams);
            var contextHash = Util.Sha3Keccack.Current.CalculateHash(contextInput);
            var context = new BigInteger(contextHash, isUnsigned: true, isBigEndian: true)
                % PrivacyPoolConstants.SNARK_SCALAR_FIELD;

            var witnessInput = new WithdrawalWitnessInput
            {
                ExistingValue = commitment.Value,
                ExistingNullifier = commitment.Nullifier,
                ExistingSecret = commitment.Secret,
                Label = commitment.Label,
                NewNullifier = newCommitment.Nullifier,
                NewSecret = newCommitment.Secret,
                WithdrawnValue = withdrawnValue,
                StateRoot = stateTree.RootAsBigInteger,
                StateTreeDepth = stateTree.Depth,
                StateSiblings = paddedStateSiblings,
                StateIndex = 0,
                ASPRoot = aspRoot,
                ASPTreeDepth = aspTree.Depth,
                ASPSiblings = paddedASPSiblings,
                ASPIndex = 0,
                Context = context
            };

            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("withdrawal"))
            {
                Assert.Fail("Withdrawal circuit artifacts not found. Cannot run withdrawal E2E.");
                return;
            }

            ICircuitArtifactSource artifactSource = circuitSource;
            var proofProvider = new PrivacyPoolProofProvider(
                new NativeProofProvider(), artifactSource);

            var proofResult = await proofProvider.GenerateWithdrawalProofAsync(witnessInput);

            Assert.NotNull(proofResult.ProofJson);
            Assert.Equal(commitment.NullifierHash, proofResult.Signals.ExistingNullifierHash);
            _output.WriteLine("  ZK withdrawal proof generated");

            var onChainProof = PrivacyPoolProofConverter.ToWithdrawProof(
                proofResult.ProofJson, proofResult.Signals);

            var relayFunction = new RelayFunction
            {
                Withdrawal = withdrawal,
                Proof = onChainProof,
                Scope = scope
            };
            var relayReceipt = await entrypointService.RelayRequestAndWaitForReceiptAsync(relayFunction);
            Assert.False(relayReceipt.HasErrors());
            _output.WriteLine("  Relay tx succeeded");

            var isSpent = await poolService.NullifierHashesQueryAsync(commitment.NullifierHash);
            Assert.True(isSpent);

            var withdrawEvents = relayReceipt.DecodeAllEvents<PoolWithdrawnEvent>();
            Assert.Single(withdrawEvents);
            Assert.Equal(commitment.NullifierHash, withdrawEvents.First().Event.SpentNullifier);

            stateTree.InsertCommitment(withdrawEvents.First().Event.NewCommitment);
            Assert.Equal(2, stateTree.Size);
            _output.WriteLine($"  Withdrawal complete. New tree size: {stateTree.Size}");
        }

        [Fact]
        [Trait("Category", "E2E")]
        public async Task Direct_Withdrawal_Cycle()
        {
            var (entrypointService, poolService) = await DeployFullStackAsync();

            var pp = PrivacyPool.FromDeployment(_web3,
                new PrivacyPoolDeploymentResult
                {
                    Entrypoint = entrypointService,
                    Pool = poolService
                },
                "test test test test test test test test test test test junk");
            await pp.InitializeAsync();

            var depositValue = Web3.Web3.Convert.ToWei(1);
            var depositResult = await pp.DepositAsync(depositValue, depositIndex: 0);
            Assert.False(depositResult.Receipt.HasErrors());
            _output.WriteLine($"  Deposited: commitment={depositResult.Commitment.CommitmentHash}");

            var stateTree = new PoseidonMerkleTree();
            stateTree.InsertCommitment(depositResult.Commitment.CommitmentHash);

            var onChainRoot = await poolService.CurrentRootQueryAsync();
            Assert.Equal(onChainRoot, stateTree.RootAsBigInteger);

            var aspTree = new PoseidonMerkleTree();
            var depositEvents = depositResult.Receipt.DecodeAllEvents<PoolDepositedEvent>();
            var label = depositEvents.First().Event.Label;
            aspTree.InsertCommitment(label);

            var aspRoot = aspTree.RootAsBigInteger;
            var updateReceipt = await entrypointService.UpdateRootRequestAndWaitForReceiptAsync(
                aspRoot, "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu");
            Assert.False(updateReceipt.HasErrors());
            _output.WriteLine($"  ASP root updated: {aspRoot}");

            var circuitSource = new Circuits.PrivacyPoolCircuitSource();
            if (!circuitSource.HasCircuit("withdrawal"))
            {
                Assert.Fail("Withdrawal circuit artifacts not found");
                return;
            }

            var proofProvider = pp.CreateProofProvider(
                new NativeProofProvider(), circuitSource);

            var withdrawnValue = depositValue / 2;
            var withdrawResult = await pp.Pool.WithdrawDirectAsync(
                depositResult.Commitment,
                0,
                withdrawnValue,
                _account.Address,
                proofProvider,
                stateTree,
                aspTree);

            Assert.False(withdrawResult.Receipt.HasErrors());
            _output.WriteLine($"  Direct withdrawal tx: {withdrawResult.Receipt.TransactionHash}");

            var isSpent = await poolService.NullifierHashesQueryAsync(
                depositResult.Commitment.NullifierHash);
            Assert.True(isSpent);

            var withdrawEvents = withdrawResult.Receipt.DecodeAllEvents<PoolWithdrawnEvent>();
            Assert.Single(withdrawEvents);
            _output.WriteLine($"  Direct withdrawal complete. Nullifier spent, event emitted.");
        }

        [Fact]
        public void Commitment_Cryptography_Offline()
        {
            var value = Web3.Web3.Convert.ToWei(1);
            var label = BigInteger.One;

            var c1 = PrivacyPoolCommitment.CreateRandom(value, label);
            var c2 = PrivacyPoolCommitment.CreateRandom(value, label);

            var tree = new PoseidonMerkleTree();
            tree.InsertCommitment(c1.CommitmentHash);
            tree.InsertCommitment(c2.CommitmentHash);
            Assert.Equal(2, tree.Size);

            for (int i = 0; i < 2; i++)
            {
                var hash = i == 0 ? c1.CommitmentHash : c2.CommitmentHash;
                var proof = tree.GenerateInclusionProof(i);
                Assert.True(tree.VerifyInclusionProof(proof, hash));
            }

            var wrongProof = tree.GenerateInclusionProof(0);
            Assert.False(tree.VerifyInclusionProof(wrongProof, c2.CommitmentHash));
        }

        private async Task<(EntrypointService entrypoint, string proxyAddress)> DeployEntrypointViaProxyAsync()
        {
            var account = _account.Address;

            var implReceipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                EntrypointDeploymentBase.BYTECODE,
                account,
                new Nethereum.Hex.HexTypes.HexBigInteger(10_000_000));

            var initFunction = new InitializeFunction { Owner = account, Postman = account };
            var initCalldata = initFunction.GetCallData();

            var proxyDeployment = new ERC1967ProxyDeployment
            {
                Implementation = implReceipt.ContractAddress,
                Data = initCalldata,
                Gas = (BigInteger)5_000_000
            };
            var proxyReceipt = await ERC1967ProxyService.DeployContractAndWaitForReceiptAsync(
                _web3, proxyDeployment);

            var service = new EntrypointService(_web3, proxyReceipt.ContractAddress);
            _output.WriteLine($"  Entrypoint (proxy): {proxyReceipt.ContractAddress}");
            return (service, proxyReceipt.ContractAddress);
        }

        private async Task<(EntrypointService entrypoint, PrivacyPoolSimpleService pool)> DeployFullStackAsync()
        {
            var (entrypointService, _) = await DeployEntrypointViaProxyAsync();

            var withdrawalVerifierService = await WithdrawalVerifierService.DeployContractAndGetServiceAsync(
                _web3, new WithdrawalVerifierDeployment());
            _output.WriteLine($"  WithdrawalVerifier: {withdrawalVerifierService.ContractAddress}");

            var commitmentVerifierService = await CommitmentVerifierService.DeployContractAndGetServiceAsync(
                _web3, new CommitmentVerifierDeployment());
            _output.WriteLine($"  CommitmentVerifier: {commitmentVerifierService.ContractAddress}");

            var poseidonT3Service = await PoseidonT3Service.DeployContractAndGetServiceAsync(
                _web3, new PoseidonT3Deployment { Gas = (BigInteger)15_000_000 });
            _output.WriteLine($"  PoseidonT3: {poseidonT3Service.ContractAddress}");

            var poseidonT4Service = await PoseidonT4Service.DeployContractAndGetServiceAsync(
                _web3, new PoseidonT4Deployment { Gas = (BigInteger)10_000_000 });
            _output.WriteLine($"  PoseidonT4: {poseidonT4Service.ContractAddress}");

            var poolDeployment = new PrivacyPoolSimpleDeployment
            {
                Entrypoint = entrypointService.ContractAddress,
                WithdrawalVerifier = withdrawalVerifierService.ContractAddress,
                RagequitVerifier = commitmentVerifierService.ContractAddress,
                Gas = (BigInteger)10_000_000
            };
            poolDeployment.LinkLibraries(poseidonT3Service.ContractAddress, poseidonT4Service.ContractAddress);

            var poolService = await PrivacyPoolSimpleService.DeployContractAndGetServiceAsync(
                _web3, poolDeployment);
            _output.WriteLine($"  Pool: {poolService.ContractAddress}");

            var registerReceipt = await entrypointService.RegisterPoolRequestAndWaitForReceiptAsync(
                NATIVE_ASSET, poolService.ContractAddress,
                BigInteger.Zero, BigInteger.Zero, BigInteger.Zero);

            if (registerReceipt.HasErrors() == true)
                throw new System.Exception("Pool registration failed");

            _output.WriteLine("  Pool registered");
            return (entrypointService, poolService);
        }

        private static BigInteger[] PadSiblings(BigInteger[] siblings, int targetLength)
        {
            var padded = new BigInteger[targetLength];
            for (int i = 0; i < siblings.Length && i < targetLength; i++)
                padded[i] = siblings[i];
            return padded;
        }
    }

    [Nethereum.ABI.FunctionEncoding.Attributes.FunctionOutput]
    public class WithdrawalContextParams
    {
        [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("tuple", "withdrawal", 1, "IPrivacyPool.Withdrawal")]
        public virtual EntrypointWithdrawal Withdrawal { get; set; } = null!;

        [Nethereum.ABI.FunctionEncoding.Attributes.Parameter("uint256", "scope", 2)]
        public virtual BigInteger Scope { get; set; }
    }
}
