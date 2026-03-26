using System.Diagnostics;
using System.Numerics;
using Nethereum.ABI;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.PrivacyPools.Circuits;
using Nethereum.PrivacyPools.Entrypoint;
using Nethereum.PrivacyPools.Entrypoint.ContractDefinition;
using Nethereum.PrivacyPools.PrivacyPoolBase;
using Nethereum.PrivacyPools.PrivacyPoolSimple;
using Nethereum.PrivacyPools.PrivacyPoolSimple.ContractDefinition;
using Nethereum.PrivacyPools.Processing;
using Nethereum.Hex.HexTypes;
using Nethereum.Web3;
using Nethereum.Web3.Accounts;
using Nethereum.ZkProofs;
using Nethereum.ZkProofs.RapidSnark;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;
using DepositFunction = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.DepositFunction;
using EntrypointWithdrawal = Nethereum.PrivacyPools.Entrypoint.ContractDefinition.Withdrawal;
using PoolDepositedEvent = Nethereum.PrivacyPools.PrivacyPoolBase.DepositedEventDTO;
using PoolWithdrawnEvent = Nethereum.PrivacyPools.PrivacyPoolBase.WithdrawnEventDTO;

namespace Nethereum.PrivacyPools.CrossSdk.Tests
{
    public class CrossSdkTests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;

        private const string TEST_MNEMONIC = "test test test test test test test test test test test junk";
        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const int GETH_CHAIN_ID = 1337;
        private const string GETH_RPC_URL = "http://127.0.0.1:18546";

        private Process? _gethProcess;
        private string? _gethDataDir;
        private IWeb3 _web3 = null!;
        private Account _account = null!;
        private PrivacyPoolDeploymentResult _deployment = null!;
        private BigInteger _scope;
        private string _scriptsDir = null!;
        private string _artifactsDir = null!;

        public CrossSdkTests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            _scriptsDir = FindScriptsDir();
            _output.WriteLine($"Scripts dir: {_scriptsDir}");

            await StartGethDevAsync();
            await DeployContractsAsync();
            await ExtractCircuitArtifactsAsync();
        }

        public async Task DisposeAsync()
        {
            if (_gethProcess != null)
            {
                try { _gethProcess.Kill(true); await _gethProcess.WaitForExitAsync(); } catch { }
                _gethProcess.Dispose();
            }
            if (_gethDataDir != null && Directory.Exists(_gethDataDir))
            {
                try { Directory.Delete(_gethDataDir, true); } catch { }
            }
            if (_artifactsDir != null && Directory.Exists(_artifactsDir))
            {
                try { Directory.Delete(_artifactsDir, true); } catch { }
            }
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        [NethereumDocExample(DocSection.Protocols, "cross-sdk", "TS SDK deposit, Nethereum ragequit")]
        public async Task TsSdkDeposit_NethereumRagequit()
        {
            var depositResult = await RunNodeScript("deposit.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                mnemonic = TEST_MNEMONIC,
                depositIndex = 0,
                valueWei = Web3.Web3.Convert.ToWei(1).ToString(),
                scope = _scope.ToString()
            });

            var tsCommitmentHash = BigInteger.Parse(depositResult["commitmentHash"]!.ToString());
            var tsNullifier = BigInteger.Parse(depositResult["nullifier"]!.ToString());
            var tsSecret = BigInteger.Parse(depositResult["secret"]!.ToString());
            var tsLabel = BigInteger.Parse(depositResult["label"]!.ToString());
            var depositValue = Web3.Web3.Convert.ToWei(1);
            _output.WriteLine($"TS deposit: commitment={tsCommitmentHash}");

            var nethCommitment = PrivacyPoolCommitment.Create(depositValue, tsLabel, tsNullifier, tsSecret);
            Assert.Equal(tsCommitmentHash, nethCommitment.CommitmentHash);
            _output.WriteLine("Commitment hash cross-verified");

            var circuitSource = new PrivacyPoolCircuitSource();
            Assert.True(circuitSource.HasCircuit("commitment"), "Commitment circuit not found");

            var proofProvider = new PrivacyPoolProofProvider(new NativeProofProvider(), circuitSource);
            var ragequitResult = await proofProvider.GenerateRagequitProofAsync(new RagequitWitnessInput
            {
                Nullifier = tsNullifier,
                Secret = tsSecret,
                Value = depositValue,
                Label = tsLabel
            });

            Assert.Equal(nethCommitment.CommitmentHash, ragequitResult.Signals.CommitmentHash);
            _output.WriteLine("Ragequit proof generated");

            var ragequitProof = PrivacyPoolProofConverter.ToRagequitProof(
                ragequitResult.ProofJson, ragequitResult.Signals);

            var ragequitReceipt = await _deployment.Pool.RagequitRequestAndWaitForReceiptAsync(
                new RagequitFunction { Proof = ragequitProof, Gas = 5_000_000 });
            Assert.False(ragequitReceipt.HasErrors());

            var isSpent = await _deployment.Pool.NullifierHashesQueryAsync(nethCommitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("TS deposit → Nethereum ragequit: SUCCESS");
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        [NethereumDocExample(DocSection.Protocols, "cross-sdk", "Nethereum deposit, TS SDK ragequit", Order = 2)]
        public async Task NethereumDeposit_TsSdkRagequit()
        {
            var (nullifier, secret, precommitment) = PrivacyPoolCommitment.GenerateRandomPrecommitment();
            var depositValue = Web3.Web3.Convert.ToWei(1);

            var depositReceipt = await _deployment.Entrypoint.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { Precommitment = precommitment, AmountToSend = depositValue });
            Assert.False(depositReceipt.HasErrors());

            var poolEvents = depositReceipt.DecodeAllEvents<PoolDepositedEvent>();
            Assert.Single(poolEvents);
            var evt = poolEvents.First().Event;

            var commitment = PrivacyPoolCommitment.Create(evt.Value, evt.Label, nullifier, secret);
            Assert.Equal(evt.Commitment, commitment.CommitmentHash);
            _output.WriteLine($"Nethereum deposit: commitment={commitment.CommitmentHash}");

            var ragequitResult = await RunNodeScript("ragequit.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                artifactsDir = _artifactsDir.Replace("\\", "/"),
                value = commitment.Value.ToString(),
                label = commitment.Label.ToString(),
                nullifier = commitment.Nullifier.ToString(),
                secret = commitment.Secret.ToString()
            });

            Assert.True(ragequitResult["success"]!.Value<bool>());
            _output.WriteLine($"TS ragequit tx: {ragequitResult["txHash"]}");

            var isSpent = await _deployment.Pool.NullifierHashesQueryAsync(commitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("Nethereum deposit → TS ragequit: SUCCESS");
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        [NethereumDocExample(DocSection.Protocols, "cross-sdk", "TS SDK deposit, Nethereum withdrawal", Order = 3)]
        public async Task TsSdkDeposit_NethereumWithdrawal()
        {
            var depositResult = await RunNodeScript("deposit.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                mnemonic = TEST_MNEMONIC,
                depositIndex = 10,
                valueWei = Web3.Web3.Convert.ToWei(1).ToString(),
                scope = _scope.ToString()
            });

            var tsCommitmentHash = BigInteger.Parse(depositResult["commitmentHash"]!.ToString());
            var tsNullifier = BigInteger.Parse(depositResult["nullifier"]!.ToString());
            var tsSecret = BigInteger.Parse(depositResult["secret"]!.ToString());
            var tsLabel = BigInteger.Parse(depositResult["label"]!.ToString());
            var scope = BigInteger.Parse(depositResult["scope"]!.ToString());
            var depositValue = Web3.Web3.Convert.ToWei(1);
            _output.WriteLine($"TS deposit: commitment={tsCommitmentHash}, label={tsLabel}");

            var stateTree = new PoseidonMerkleTree();
            stateTree.InsertCommitment(tsCommitmentHash);
            var onChainRoot = await _deployment.Pool.CurrentRootQueryAsync();
            Assert.Equal(onChainRoot, stateTree.RootAsBigInteger);
            _output.WriteLine("State tree root verified");

            var aspTree = new PoseidonMerkleTree();
            aspTree.InsertCommitment(tsLabel);
            var aspRoot = aspTree.RootAsBigInteger;

            var updateRootReceipt = await _deployment.Entrypoint.UpdateRootRequestAndWaitForReceiptAsync(
                aspRoot, "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu");
            Assert.False(updateRootReceipt.HasErrors());
            _output.WriteLine("ASP root updated");

            var withdrawnValue = depositValue / 2;
            var newCommitment = PrivacyPoolCommitment.CreateRandom(
                depositValue - withdrawnValue, tsLabel);

            var stateMerkleProof = stateTree.GenerateInclusionProof(0);
            var stateSiblings = PadSiblings(stateTree.GetProofSiblings(stateMerkleProof), 32);
            var aspMerkleProof = aspTree.GenerateInclusionProof(0);
            var aspSiblings = PadSiblings(aspTree.GetProofSiblings(aspMerkleProof), 32);

            var relayData = new ABIEncode().GetABIEncoded(
                new ABIValue("address", _account.Address),
                new ABIValue("address", _account.Address),
                new ABIValue("uint256", BigInteger.Zero));

            var withdrawal = new EntrypointWithdrawal
            {
                Processooor = _deployment.Entrypoint.ContractAddress,
                Data = relayData
            };

            var context = WithdrawalContextHelper.ComputeContext(withdrawal, scope);

            var witnessInput = new WithdrawalWitnessInput
            {
                ExistingValue = depositValue,
                ExistingNullifier = tsNullifier,
                ExistingSecret = tsSecret,
                Label = tsLabel,
                NewNullifier = newCommitment.Nullifier,
                NewSecret = newCommitment.Secret,
                WithdrawnValue = withdrawnValue,
                StateRoot = stateTree.RootAsBigInteger,
                StateTreeDepth = stateTree.Depth,
                StateSiblings = stateSiblings,
                StateIndex = 0,
                ASPRoot = aspRoot,
                ASPTreeDepth = aspTree.Depth,
                ASPSiblings = aspSiblings,
                ASPIndex = 0,
                Context = context
            };

            var circuitSource = new PrivacyPoolCircuitSource();
            Assert.True(circuitSource.HasCircuit("withdrawal"), "Withdrawal circuit not found");

            var proofProvider = new PrivacyPoolProofProvider(new NativeProofProvider(), circuitSource);
            var proofResult = await proofProvider.GenerateWithdrawalProofAsync(witnessInput);
            _output.WriteLine("Withdrawal proof generated");

            var onChainProof = PrivacyPoolProofConverter.ToWithdrawProof(
                proofResult.ProofJson, proofResult.Signals);

            var relayReceipt = await _deployment.Entrypoint.RelayRequestAndWaitForReceiptAsync(
                new RelayFunction { Withdrawal = withdrawal, Proof = onChainProof, Scope = scope });
            Assert.False(relayReceipt.HasErrors());

            var tsNullifierHash = PrivacyPoolCommitment.Create(
                depositValue, tsLabel, tsNullifier, tsSecret).NullifierHash;
            var isSpent = await _deployment.Pool.NullifierHashesQueryAsync(tsNullifierHash);
            Assert.True(isSpent);

            var withdrawEvents = relayReceipt.DecodeAllEvents<PoolWithdrawnEvent>();
            Assert.Single(withdrawEvents);
            _output.WriteLine("TS deposit → Nethereum withdrawal: SUCCESS");
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        [NethereumDocExample(DocSection.Protocols, "cross-sdk", "Nethereum deposit, TS SDK withdrawal", Order = 4)]
        public async Task NethereumDeposit_TsSdkWithdrawal()
        {
            var pp = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp.InitializeAsync();
            var scope = pp.Scope;

            var depositValue = Web3.Web3.Convert.ToWei(1);
            var depositResult = await pp.DepositAsync(depositValue, depositIndex: 20);
            Assert.False(depositResult.Receipt.HasErrors());
            _output.WriteLine($"Nethereum deposit: commitment={depositResult.Commitment.CommitmentHash}");

            var poolDepositEvents = depositResult.Receipt.DecodeAllEvents<PoolDepositedEvent>();
            Assert.Single(poolDepositEvents);
            var myDeposit = poolDepositEvents.First().Event;
            _output.WriteLine($"Event decoded: label={myDeposit.Label}");

            var stateTree = new PoseidonMerkleTree();
            stateTree.InsertCommitment(depositResult.Commitment.CommitmentHash);

            var aspTree = new PoseidonMerkleTree();
            aspTree.InsertCommitment(myDeposit.Label);
            var aspRoot = aspTree.RootAsBigInteger;

            var updateReceipt = await _deployment.Entrypoint.UpdateRootRequestAndWaitForReceiptAsync(
                aspRoot, "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu");
            Assert.False(updateReceipt.HasErrors());
            _output.WriteLine("ASP root updated");

            var stateLeaves = new[] { depositResult.Commitment.CommitmentHash.ToString() };
            var myLeafIndex = 0;

            var aspLeaves = new[] { myDeposit.Label.ToString() };

            var withdrawnValue = depositValue / 2;

            var withdrawResult = await RunNodeScript("withdraw.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                artifactsDir = _artifactsDir.Replace("\\", "/"),
                mnemonic = TEST_MNEMONIC,
                scope = scope.ToString(),
                existingValue = depositResult.Commitment.Value.ToString(),
                existingLabel = myDeposit.Label.ToString(),
                existingNullifier = depositResult.Commitment.Nullifier.ToString(),
                existingSecret = depositResult.Commitment.Secret.ToString(),
                stateLeaves,
                stateLeafIndex = myLeafIndex.ToString(),
                aspLeaves,
                aspLeafIndex = "0",
                withdrawnValue = withdrawnValue.ToString(),
                recipientAddress = _account.Address,
                relayerAddress = _account.Address,
                relayFeeBps = "0"
            });

            Assert.True(withdrawResult["success"]!.Value<bool>(),
                $"TS withdrawal failed. Script stderr may have details.");
            _output.WriteLine($"TS withdrawal tx: {withdrawResult["txHash"]}");

            var isSpent = await _deployment.Pool.NullifierHashesQueryAsync(
                depositResult.Commitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("Nethereum deposit → TS withdrawal: SUCCESS");
        }

        [Fact]
        [Trait("Category", "CrossSdk-ERC20")]
        [NethereumDocExample(DocSection.Protocols, "cross-sdk-erc20", "TS SDK ERC20 deposit, Nethereum ragequit", Order = 5)]
        public async Task TsSdkERC20Deposit_NethereumRagequit()
        {
            var (erc20Deployment, tokenAddress, erc20Scope) = await DeployERC20PoolAsync();

            var depositResult = await RunNodeScript("deposit-erc20.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = erc20Deployment.Entrypoint.ContractAddress,
                poolAddress = erc20Deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                mnemonic = TEST_MNEMONIC,
                depositIndex = 0,
                valueWei = Web3.Web3.Convert.ToWei(100).ToString(),
                scope = erc20Scope.ToString(),
                tokenAddress
            });

            var tsCommitmentHash = BigInteger.Parse(depositResult["commitmentHash"]!.ToString());
            var tsNullifier = BigInteger.Parse(depositResult["nullifier"]!.ToString());
            var tsSecret = BigInteger.Parse(depositResult["secret"]!.ToString());
            var tsLabel = BigInteger.Parse(depositResult["label"]!.ToString());
            var depositValue = Web3.Web3.Convert.ToWei(100);
            _output.WriteLine($"TS ERC20 deposit: commitment={tsCommitmentHash}");

            var nethCommitment = PrivacyPoolCommitment.Create(depositValue, tsLabel, tsNullifier, tsSecret);
            Assert.Equal(tsCommitmentHash, nethCommitment.CommitmentHash);
            _output.WriteLine("ERC20 commitment hash cross-verified");

            var circuitSource = new PrivacyPoolCircuitSource();
            Assert.True(circuitSource.HasCircuit("commitment"), "Commitment circuit not found");

            var proofProvider = new PrivacyPoolProofProvider(new NativeProofProvider(), circuitSource);
            var ragequitResult = await proofProvider.GenerateRagequitProofAsync(new RagequitWitnessInput
            {
                Nullifier = tsNullifier,
                Secret = tsSecret,
                Value = depositValue,
                Label = tsLabel
            });

            var ragequitProof = PrivacyPoolProofConverter.ToRagequitProof(
                ragequitResult.ProofJson, ragequitResult.Signals);

            var ragequitReceipt = await erc20Deployment.Pool.RagequitRequestAndWaitForReceiptAsync(
                new RagequitFunction { Proof = ragequitProof, Gas = 5_000_000 });
            Assert.False(ragequitReceipt.HasErrors());

            var isSpent = await erc20Deployment.Pool.NullifierHashesQueryAsync(nethCommitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("TS ERC20 deposit → Nethereum ragequit: SUCCESS");
        }

        [Fact]
        [Trait("Category", "CrossSdk-ERC20")]
        [NethereumDocExample(DocSection.Protocols, "cross-sdk-erc20", "Nethereum ERC20 deposit, TS SDK ragequit", Order = 6)]
        public async Task NethereumERC20Deposit_TsSdkRagequit()
        {
            var (erc20Deployment, tokenAddress, erc20Scope) = await DeployERC20PoolAsync();

            var pp = new PrivacyPool(_web3,
                erc20Deployment.Entrypoint.ContractAddress,
                erc20Deployment.Pool.ContractAddress,
                TEST_MNEMONIC);
            await pp.InitializeAsync();

            var depositValue = Web3.Web3.Convert.ToWei(100);
            await pp.ApproveERC20Async(tokenAddress, depositValue);
            var depositResult = await pp.DepositERC20Async(tokenAddress, depositValue, depositIndex: 0);
            Assert.False(depositResult.Receipt.HasErrors());
            _output.WriteLine($"Nethereum ERC20 deposit: commitment={depositResult.Commitment.CommitmentHash}");

            var ragequitResult = await RunNodeScript("ragequit.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                poolAddress = erc20Deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                artifactsDir = _artifactsDir.Replace("\\", "/"),
                value = depositResult.Commitment.Value.ToString(),
                label = depositResult.Commitment.Label.ToString(),
                nullifier = depositResult.Commitment.Nullifier.ToString(),
                secret = depositResult.Commitment.Secret.ToString()
            });

            Assert.True(ragequitResult["success"]!.Value<bool>());
            _output.WriteLine($"TS ragequit tx: {ragequitResult["txHash"]}");

            var isSpent = await erc20Deployment.Pool.NullifierHashesQueryAsync(
                depositResult.Commitment.NullifierHash);
            Assert.True(isSpent);
            _output.WriteLine("Nethereum ERC20 deposit → TS ragequit: SUCCESS");
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        public async Task Safe_TsSdkDeposit_MatchesNethereumSafeKeys()
        {
            // TS SDK v1.2.0 deposit.mjs now uses bytesToBigInt (safe keys)
            var depositResult = await RunNodeScript("deposit.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                mnemonic = TEST_MNEMONIC,
                depositIndex = 20,
                valueWei = Web3.Web3.Convert.ToWei(1).ToString(),
                scope = _scope.ToString()
            });

            var tsMasterNullifier = BigInteger.Parse(depositResult["masterNullifier"]!.ToString());
            var tsMasterSecret = BigInteger.Parse(depositResult["masterSecret"]!.ToString());

            // Nethereum safe account (default constructor) should match TS SDK v1.2.0
            var safeAccount = new PrivacyPoolAccount(TEST_MNEMONIC);
            Assert.Equal(safeAccount.MasterNullifier, tsMasterNullifier);
            Assert.Equal(safeAccount.MasterSecret, tsMasterSecret);

            var tsCommitmentHash = BigInteger.Parse(depositResult["commitmentHash"]!.ToString());
            var tsNullifier = BigInteger.Parse(depositResult["nullifier"]!.ToString());
            var tsSecret = BigInteger.Parse(depositResult["secret"]!.ToString());
            var tsLabel = BigInteger.Parse(depositResult["label"]!.ToString());
            var depositValue = Web3.Web3.Convert.ToWei(1);

            var nethCommitment = PrivacyPoolCommitment.Create(depositValue, tsLabel, tsNullifier, tsSecret);
            Assert.Equal(tsCommitmentHash, nethCommitment.CommitmentHash);
            _output.WriteLine("Safe keys: TS SDK v1.2.0 deposit matches Nethereum safe derivation");
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        public async Task Legacy_TsSdkDeposit_MatchesNethereumLegacyKeys()
        {
            // Legacy deposit script uses bytesToNumber (53-bit lossy)
            var depositResult = await RunNodeScript("deposit-legacy.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                mnemonic = TEST_MNEMONIC,
                depositIndex = 21,
                valueWei = Web3.Web3.Convert.ToWei(1).ToString(),
                scope = _scope.ToString()
            });

            var tsMasterNullifier = BigInteger.Parse(depositResult["masterNullifier"]!.ToString());
            var tsMasterSecret = BigInteger.Parse(depositResult["masterSecret"]!.ToString());

            // Nethereum legacy account should match TS SDK legacy derivation
            var legacyAccount = PrivacyPoolAccount.CreateLegacy(TEST_MNEMONIC);
            Assert.Equal(legacyAccount.MasterNullifier, tsMasterNullifier);
            Assert.Equal(legacyAccount.MasterSecret, tsMasterSecret);

            var tsCommitmentHash = BigInteger.Parse(depositResult["commitmentHash"]!.ToString());
            var tsNullifier = BigInteger.Parse(depositResult["nullifier"]!.ToString());
            var tsSecret = BigInteger.Parse(depositResult["secret"]!.ToString());
            var tsLabel = BigInteger.Parse(depositResult["label"]!.ToString());
            var depositValue = Web3.Web3.Convert.ToWei(1);

            var nethCommitment = PrivacyPoolCommitment.Create(depositValue, tsLabel, tsNullifier, tsSecret);
            Assert.Equal(tsCommitmentHash, nethCommitment.CommitmentHash);
            _output.WriteLine("Legacy keys: TS SDK legacy deposit matches Nethereum legacy derivation");
        }

        [Fact]
        [Trait("Category", "CrossSdk")]
        public async Task Legacy_TsSdkMigrationChain_NethereumRecoversSafeSpendableAccount()
        {
            var depositValue = Web3.Web3.Convert.ToWei(1);
            var postMigrationValue = depositValue / 2;

            var legacyDeposit = await RunNodeScript("deposit-legacy.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                mnemonic = TEST_MNEMONIC,
                depositIndex = 30,
                valueWei = depositValue.ToString(),
                scope = _scope.ToString()
            });

            var legacyCommitmentHash = BigInteger.Parse(legacyDeposit["commitmentHash"]!.ToString());
            var legacyLabel = BigInteger.Parse(legacyDeposit["label"]!.ToString());
            var legacyNullifier = legacyDeposit["nullifier"]!.ToString();
            var legacySecret = legacyDeposit["secret"]!.ToString();
            _output.WriteLine($"Legacy TS deposit: commitment={legacyCommitmentHash}, label={legacyLabel}");

            var aspTree = new PoseidonMerkleTree();
            aspTree.InsertCommitment(legacyLabel);
            var aspRoot = aspTree.RootAsBigInteger;

            var aspReceipt = await _deployment.Entrypoint.UpdateRootRequestAndWaitForReceiptAsync(
                aspRoot, "bafybeigdyrzt5sfp7udm7hu76uh7y26nf3efuylqabf3okuzefo5ij6neu");
            Assert.False(aspReceipt.HasErrors());

            var stateTree = new PoseidonMerkleTree();
            stateTree.InsertCommitment(legacyCommitmentHash);
            Assert.Equal(await _deployment.Pool.CurrentRootQueryAsync(), stateTree.RootAsBigInteger);

            var migrationResult = await RunNodeScript("withdraw.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                artifactsDir = _artifactsDir.Replace("\\", "/"),
                mnemonic = TEST_MNEMONIC,
                scope = _scope.ToString(),
                existingValue = depositValue.ToString(),
                existingLabel = legacyLabel.ToString(),
                existingNullifier = legacyNullifier,
                existingSecret = legacySecret,
                stateLeaves = new[] { legacyCommitmentHash.ToString() },
                stateLeafIndex = "0",
                aspLeaves = new[] { legacyLabel.ToString() },
                aspLeafIndex = "0",
                withdrawnValue = BigInteger.Zero.ToString(),
                recipientAddress = _account.Address,
                relayerAddress = _account.Address,
                relayFeeBps = "0",
                childIndex = "0"
            });

            Assert.True(migrationResult["success"]!.Value<bool>());
            var migratedCommitmentHash = BigInteger.Parse(migrationResult["newCommitmentHash"]!.ToString());
            _output.WriteLine($"TS migration commitment: {migratedCommitmentHash}");

            stateTree.InsertCommitment(migratedCommitmentHash);
            Assert.Equal(await _deployment.Pool.CurrentRootQueryAsync(), stateTree.RootAsBigInteger);

            var postMigrationResult = await RunNodeScript("withdraw.mjs", new
            {
                rpcUrl = GETH_RPC_URL,
                chainId = GETH_CHAIN_ID,
                entrypointAddress = _deployment.Entrypoint.ContractAddress,
                poolAddress = _deployment.Pool.ContractAddress,
                privateKey = OWNER_PRIVATE_KEY,
                artifactsDir = _artifactsDir.Replace("\\", "/"),
                mnemonic = TEST_MNEMONIC,
                scope = _scope.ToString(),
                existingValue = depositValue.ToString(),
                existingLabel = legacyLabel.ToString(),
                existingNullifier = migrationResult["newNullifier"]!.ToString(),
                existingSecret = migrationResult["newSecret"]!.ToString(),
                stateLeaves = new[]
                {
                    legacyCommitmentHash.ToString(),
                    migratedCommitmentHash.ToString()
                },
                stateLeafIndex = "1",
                aspLeaves = new[] { legacyLabel.ToString() },
                aspLeafIndex = "0",
                withdrawnValue = (depositValue - postMigrationValue).ToString(),
                recipientAddress = _account.Address,
                relayerAddress = _account.Address,
                relayFeeBps = "0",
                childIndex = "1"
            });

            Assert.True(postMigrationResult["success"]!.Value<bool>());
            var postMigrationCommitmentHash = BigInteger.Parse(
                postMigrationResult["newCommitmentHash"]!.ToString());
            _output.WriteLine($"TS post-migration commitment: {postMigrationCommitmentHash}");

            stateTree.InsertCommitment(postMigrationCommitmentHash);
            Assert.Equal(await _deployment.Pool.CurrentRootQueryAsync(), stateTree.RootAsBigInteger);

            var pp = PrivacyPool.FromDeployment(_web3, _deployment, TEST_MNEMONIC);
            await pp.InitializeAsync();

            var sync = await pp.SyncFromChainAsync();
            Assert.Equal(2, sync.PoolAccounts.Count);

            var recoveredLegacy = sync.PoolAccounts.Single(
                account => account.Deposit.Commitment.CommitmentHash == legacyCommitmentHash);
            Assert.True(recoveredLegacy.IsMigrated);
            Assert.False(recoveredLegacy.IsSpendable);
            Assert.Single(recoveredLegacy.Withdrawals);
            Assert.True(recoveredLegacy.Withdrawals[0].IsMigration);

            var recoveredSafe = sync.PoolAccounts.Single(
                account => account.Deposit.Commitment.CommitmentHash == migratedCommitmentHash);
            Assert.False(recoveredSafe.IsMigrated);
            Assert.Equal(migratedCommitmentHash, recoveredSafe.Deposit.Commitment.CommitmentHash);
            Assert.Equal(postMigrationCommitmentHash, recoveredSafe.LatestCommitment.Commitment.CommitmentHash);
            Assert.Equal(postMigrationValue, recoveredSafe.SpendableValue);
            Assert.Equal(2, recoveredSafe.Withdrawals.Count);
            Assert.True(recoveredSafe.IsSpendable);

            var spendable = pp.GetSpendableAccounts();
            Assert.Single(spendable);
            Assert.Equal(postMigrationCommitmentHash, spendable[0].LatestCommitment.Commitment.CommitmentHash);
        }

        private const string FUZZ_ERC20_BYTECODE = "0x608060405234801561000f575f5ffd5b50604080518082018252600980825268046757a7a45524332360bc1b602080840182905284518086019095529184529083015290600361004f838261010e565b50600461005c828261010e565b5050600580546001600160a01b03191633179055506101c8565b634e487b7160e01b5f52604160045260245ffd5b600181811c9082168061009e57607f821691505b6020821081036100bc57634e487b7160e01b5f52602260045260245ffd5b50919050565b601f82111561010957805f5260205f20601f840160051c810160208510156100e75750805b601f840160051c820191505b81811015610106575f81556001016100f3565b50505b505050565b81516001600160401b0381111561012757610127610076565b61013b81610135845461008a565b846100c2565b6020601f82116001811461016d575f83156101565750848201515b5f19600385901b1c1916600184901b178455610106565b5f84815260208120601f198516915b8281101561019c578785015182556020948501946001909201910161017c565b50848210156101b957868401515f19600387901b60f8161c191681555b50505050600190811b01905550565b610a22806101d55f395ff3fe608060405234801561000f575f5ffd5b50600436106100c4575f3560e01c806340c10f191161007d5780639dc29fac116100585780639dc29fac1461018f578063a9059cbb146101a2578063dd62ed3e146101b5575f5ffd5b806340c10f191461013d57806370a082311461015257806395d89b4114610187575f5ffd5b806318160ddd116100ad57806318160ddd1461010957806323b872dd1461011b578063313ce5671461012e575f5ffd5b806306fdde03146100c8578063095ea7b3146100e6575b5f5ffd5b6100d06101fa565b6040516100dd919061086b565b60405180910390f35b6100f96100f43660046108e6565b61028a565b60405190151581526020016100dd565b6002545b6040519081526020016100dd565b6100f961012936600461090e565b6102a3565b604051601281526020016100dd565b61015061014b3660046108e6565b6102c6565b005b61010d610160366004610948565b73ffffffffffffffffffffffffffffffffffffffff165f9081526020819052604090205490565b6100d06102d4565b61015061019d3660046108e6565b6102e3565b6100f96101b03660046108e6565b6102ed565b61010d6101c3366004610968565b73ffffffffffffffffffffffffffffffffffffffff9182165f90815260016020908152604080832093909416825291909152205490565b60606003805461020990610999565b80601f016020809104026020016040519081016040528092919081815260200182805461023590610999565b80156102805780601f1061025757610100808354040283529160200191610280565b820191905f5260205f20905b81548152906001019060200180831161026357829003601f168201915b5050505050905090565b5f3361029781858561033e565b60019150505b92915050565b5f336102b0858285610350565b6102bb858585610422565b506001949350505050565b6102d082826104cb565b5050565b60606004805461020990610999565b6102d08282610525565b6005545f90339073ffffffffffffffffffffffffffffffffffffffff168103610333576005546103339073ffffffffffffffffffffffffffffffffffffffff16846104cb565b610297818585610422565b61034b838383600161057f565b505050565b73ffffffffffffffffffffffffffffffffffffffff8381165f908152600160209081526040808320938616835292905220547fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffff811461041c578181101561040e576040517ffb8f41b200000000000000000000000000000000000000000000000000000000815273ffffffffffffffffffffffffffffffffffffffff8416600482015260248101829052604481018390526064015b60405180910390fd5b61041c84848484035f61057f565b50505050565b73ffffffffffffffffffffffffffffffffffffffff8316610471576040517f96c6fd1e0000000000000000000000000000000000000000000000000000000081525f6004820152602401610405565b73ffffffffffffffffffffffffffffffffffffffff82166104c0576040517fec442f050000000000000000000000000000000000000000000000000000000081525f6004820152602401610405565b61034b8383836106c4565b73ffffffffffffffffffffffffffffffffffffffff821661051a576040517fec442f050000000000000000000000000000000000000000000000000000000081525f6004820152602401610405565b6102d05f83836106c4565b73ffffffffffffffffffffffffffffffffffffffff8216610574576040517f96c6fd1e0000000000000000000000000000000000000000000000000000000081525f6004820152602401610405565b6102d0825f836106c4565b73ffffffffffffffffffffffffffffffffffffffff84166105ce576040517fe602df050000000000000000000000000000000000000000000000000000000081525f6004820152602401610405565b73ffffffffffffffffffffffffffffffffffffffff831661061d576040517f94280d620000000000000000000000000000000000000000000000000000000081525f6004820152602401610405565b73ffffffffffffffffffffffffffffffffffffffff8085165f908152600160209081526040808320938716835292905220829055801561041c578273ffffffffffffffffffffffffffffffffffffffff168473ffffffffffffffffffffffffffffffffffffffff167f8c5be1e5ebec7d5bd14f71427d1e84f3dd0314c0f7b2291e5b200ac8c7c3b925846040516106b691815260200190565b60405180910390a350505050565b73ffffffffffffffffffffffffffffffffffffffff83166106fb578060025f8282546106f091906109ea565b909155506107ab9050565b73ffffffffffffffffffffffffffffffffffffffff83165f9081526020819052604090205481811015610780576040517fe450d38c00000000000000000000000000000000000000000000000000000000815273ffffffffffffffffffffffffffffffffffffffff851660048201526024810182905260448101839052606401610405565b73ffffffffffffffffffffffffffffffffffffffff84165f9081526020819052604090209082900390555b73ffffffffffffffffffffffffffffffffffffffff82166107d4576002805482900390556107ff565b73ffffffffffffffffffffffffffffffffffffffff82165f9081526020819052604090208054820190555b8173ffffffffffffffffffffffffffffffffffffffff168373ffffffffffffffffffffffffffffffffffffffff167fddf252ad1be2c89b69c2b068fc378daa952ba7f163c4a11628f55a4df523b3ef8360405161085e91815260200190565b60405180910390a3505050565b602081525f82518060208401528060208501604085015e5f6040828501015260407fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe0601f83011684010191505092915050565b803573ffffffffffffffffffffffffffffffffffffffff811681146108e1575f5ffd5b919050565b5f5f604083850312156108f7575f5ffd5b610900836108be565b946020939093013593505050565b5f5f5f60608486031215610920575f5ffd5b610929846108be565b9250610937602085016108be565b929592945050506040919091013590565b5f60208284031215610958575f5ffd5b610961826108be565b9392505050565b5f5f60408385031215610979575f5ffd5b610982836108be565b9150610990602084016108be565b90509250929050565b600181811c908216806109ad57607f821691505b6020821081036109e4577f4e487b71000000000000000000000000000000000000000000000000000000005f52602260045260245ffd5b50919050565b8082018082111561029d577f4e487b71000000000000000000000000000000000000000000000000000000005f52601160045260245ffd";

        private async Task<(PrivacyPoolERC20DeploymentResult deployment, string tokenAddress, BigInteger scope)> DeployERC20PoolAsync()
        {
            var tokenReceipt = await _web3.Eth.DeployContract.SendRequestAndWaitForReceiptAsync(
                FUZZ_ERC20_BYTECODE, _account.Address, new HexBigInteger(3_000_000));
            var tokenAddress = tokenReceipt.ContractAddress!;
            _output.WriteLine($"ERC20 Token: {tokenAddress}");

            var mintAmount = Web3.Web3.Convert.ToWei(1_000_000);
            var mintCalldata = "0x40c10f19" + new Nethereum.ABI.ABIEncode().GetABIEncoded(
                new Nethereum.ABI.ABIValue("address", _account.Address),
                new Nethereum.ABI.ABIValue("uint256", mintAmount)).ToHex();
            var mintTxInput = new Nethereum.RPC.Eth.DTOs.TransactionInput(
                mintCalldata, tokenAddress, _account.Address,
                new HexBigInteger(500_000), new HexBigInteger(0));
            var mintTxHash = await _web3.Eth.TransactionManager.SendTransactionAsync(mintTxInput);
            await _web3.TransactionReceiptPolling.PollForReceiptAsync(mintTxHash);
            _output.WriteLine($"Minted {Web3.Web3.Convert.FromWei(mintAmount)} tokens");

            var erc20Deployment = await PrivacyPoolDeployer.DeployERC20FullStackAsync(_web3,
                new PrivacyPoolERC20DeploymentConfig
                {
                    OwnerAddress = _account.Address,
                    TokenAddress = tokenAddress
                });
            var erc20Scope = await erc20Deployment.Pool.ScopeQueryAsync();
            _output.WriteLine($"ERC20 Entrypoint: {erc20Deployment.Entrypoint.ContractAddress}");
            _output.WriteLine($"ERC20 Pool: {erc20Deployment.Pool.ContractAddress}");
            _output.WriteLine($"ERC20 Scope: {erc20Scope}");

            return (erc20Deployment, tokenAddress, erc20Scope);
        }

        private async Task<JObject> RunNodeScript(string scriptName, object input)
        {
            _output.WriteLine($"Running {scriptName}...");
            var timeoutMs = scriptName.Contains("deposit") ? 120_000 : 300_000;
            var result = await NodeRunner.RunAsync(_scriptsDir, scriptName, input, timeoutMs);

            if (!string.IsNullOrWhiteSpace(result.Stderr))
                _output.WriteLine($"  stderr: {result.Stderr.Trim()}");

            if (!result.Success)
                throw new Exception(
                    $"Script {scriptName} failed (exit {result.ExitCode}).\n" +
                    $"stdout: {result.Stdout}\nstderr: {result.Stderr}");

            return result.ParseJson();
        }

        private async Task StartGethDevAsync()
        {
            var gethExe = FindGethExe();
            if (!File.Exists(gethExe))
                throw new FileNotFoundException($"geth.exe not found at {gethExe}");

            _gethDataDir = Path.Combine(Path.GetTempPath(), $"geth-crosssdk-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_gethDataDir);

            _gethProcess = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = gethExe,
                    Arguments = $"--dev --dev.period 1 --dev.gaslimit 30000000 " +
                                $"--datadir \"{_gethDataDir}\" " +
                                $"--http --http.addr 127.0.0.1 --http.port 18546 " +
                                $"--http.api eth,net,web3,debug,personal,admin " +
                                $"--verbosity 2",
                    UseShellExecute = false,
                    RedirectStandardOutput = false,
                    RedirectStandardError = false,
                    CreateNoWindow = true
                }
            };
            _gethProcess.Start();
            _output.WriteLine($"Geth dev started (PID {_gethProcess.Id})");

            _account = new Account(OWNER_PRIVATE_KEY, GETH_CHAIN_ID);
            _account.TransactionManager.UseLegacyAsDefault = true;
            _web3 = new Web3.Web3(_account, GETH_RPC_URL);

            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < 30)
            {
                try
                {
                    await ((Web3.Web3)_web3).Eth.ChainId.SendRequestAsync();
                    _output.WriteLine("Geth ready");
                    break;
                }
                catch { await Task.Delay(500); }
            }

            var balance = await _web3.Eth.GetBalance.SendRequestAsync(_account.Address);
            if (balance.Value == 0)
            {
                var adminWeb3 = new Web3.Web3(GETH_RPC_URL);
                var accounts = await adminWeb3.Eth.Accounts.SendRequestAsync();
                var fundAmount = Web3.Web3.Convert.ToWei(10000);
                var txHash = await adminWeb3.Eth.TransactionManager.SendTransactionAsync(
                    accounts![0], _account.Address, new Hex.HexTypes.HexBigInteger(fundAmount));
                await adminWeb3.TransactionReceiptPolling.PollForReceiptAsync(txHash);
                _output.WriteLine("Account funded");
            }
        }

        private async Task DeployContractsAsync()
        {
            _deployment = await PrivacyPoolDeployer.DeployFullStackAsync(_web3,
                new PrivacyPoolDeploymentConfig { OwnerAddress = _account.Address });
            _scope = await _deployment.Pool.ScopeQueryAsync();
            _output.WriteLine($"Entrypoint: {_deployment.Entrypoint.ContractAddress}");
            _output.WriteLine($"Pool: {_deployment.Pool.ContractAddress}");
            _output.WriteLine($"Scope: {_scope}");
        }

        private async Task ExtractCircuitArtifactsAsync()
        {
            _artifactsDir = Path.Combine(Path.GetTempPath(), $"crosssdk-artifacts-{Guid.NewGuid():N}");
            Directory.CreateDirectory(_artifactsDir);

            var source = new PrivacyPoolCircuitSource();
            foreach (var artifact in new[]
            {
                (SourceCircuit: "commitment", OutputCircuit: "commitment"),
                (SourceCircuit: "withdrawal", OutputCircuit: "withdraw")
            })
            {
                if (!source.HasCircuit(artifact.SourceCircuit))
                {
                    _output.WriteLine($"WARNING: {artifact.SourceCircuit} circuit not available");
                    continue;
                }
                var wasm = await source.GetWasmAsync(artifact.SourceCircuit);
                var zkey = await source.GetZkeyAsync(artifact.SourceCircuit);
                await File.WriteAllBytesAsync(Path.Combine(_artifactsDir, $"{artifact.OutputCircuit}.wasm"), wasm);
                await File.WriteAllBytesAsync(Path.Combine(_artifactsDir, $"{artifact.OutputCircuit}.zkey"), zkey);
                _output.WriteLine(
                    $"Extracted {artifact.OutputCircuit} artifacts ({wasm.Length + zkey.Length} bytes)");
            }
        }

        private static string FindScriptsDir()
        {
            var dir = AppDomain.CurrentDomain.BaseDirectory;
            while (dir != null)
            {
                var candidate = Path.Combine(dir, "tests", "Nethereum.PrivacyPools.CrossSdk.Tests", "scripts", "node_modules");
                if (Directory.Exists(candidate))
                    return Path.Combine(dir, "tests", "Nethereum.PrivacyPools.CrossSdk.Tests", "scripts");

                dir = Path.GetDirectoryName(dir);
            }
            throw new DirectoryNotFoundException(
                "Could not find scripts directory with node_modules. Run 'npm install' in tests/Nethereum.PrivacyPools.CrossSdk.Tests/scripts/");
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
            return "geth";
        }

        private static BigInteger[] PadSiblings(BigInteger[] siblings, int targetLength)
        {
            var padded = new BigInteger[targetLength];
            for (int i = 0; i < siblings.Length && i < targetLength; i++)
                padded[i] = siblings[i];
            return padded;
        }
    }
}
