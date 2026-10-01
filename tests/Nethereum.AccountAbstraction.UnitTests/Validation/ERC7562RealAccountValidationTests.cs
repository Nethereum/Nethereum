using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.ABI.Model;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Gas;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    /// <summary>
    /// Proves the ERC7562SimulationService fix end to end: the simulator must ABI-encode
    /// the real validateUserOp(userOp, userOpHash, missingAccountFunds) calldata so the
    /// account's actual validation logic executes, instead of sending only the 4-byte
    /// selector (which the account's argument decoding turns into an all-zero/short-circuited
    /// call that reverts or no-ops before any interesting opcode runs, leaving the ERC-7562
    /// trace empty and the engine vacuously "valid").
    ///
    /// The three tests below drive REAL bytecode through the EVM (no synthetic traces):
    ///  - the positive test deploys a genuine, already-initialized SimpleAccount proxy (real
    ///    EntryPoint + SimpleAccountFactory + SenderCreator) and validates a correctly-signed op;
    ///  - the negative test uses a minimal account whose bytecode only reaches a banned
    ///    opcode (TIMESTAMP) once it has been given calldata longer than a bare selector,
    ///    so it can only be tripped by the fix (full ABI-encoded calldata);
    ///  - the counterfactual test drives an UNDEPLOYED sender through InitCode-based
    ///    deployment (see NOTE below).
    ///
    /// NOTE — counterfactual (initCode) deployment fix: ERC7562SimulationService.
    /// SimulateValidationPhaseAsync used to invoke the factory phase with
    /// `from = entryPointAddress` directly. A real v0.7+ SimpleAccountFactory rejects that —
    /// createAccount reverts with NotSenderCreator unless msg.sender is EntryPoint's own
    /// senderCreator() helper contract, which is what actually calls factories during a real
    /// handleOps. So the simulator's counterfactual (initCode-based) deployment phase never
    /// actually deployed the sender against a real v0.7+ factory: the factory call reverted,
    /// SimulateWithTransactionExecutorAsync didn't check the call's success/failure, and the
    /// subsequent sender-phase call found no code — reported as a false OP-041 "EXTCODE
    /// access to address without code" instead of a deployment failure.
    /// Fixed by: (1) resolving EntryPoint.senderCreator() and using it as the factory caller
    /// (falling back to entryPointAddress for older EntryPoints without it); (2) surfacing a
    /// genuine factory revert as an AA13 violation instead of falling through to the sender
    /// phase; (3) confirming ExecutionMode.Call commits its state changes into the shared
    /// ExecutionStateService on success (TransactionExecutor.ExecuteCode calls
    /// CommitSnapshot, not a rollback), so the CREATE2-deployed sender's code is visible to
    /// the very next phase without any extra plumbing.
    /// Fixing the above exposed a second, closely related defect in
    /// ERC7562TracingInterceptor: CREATE/CREATE2 were validated twice per occurrence — once
    /// immediately and correctly (via OnCreate), and again one opcode later via the generic
    /// "validate previous opcode" lookback, by which point CurrentEntity/CurrentAddress had
    /// already advanced to the newly deployed contract's own constructor — turning one
    /// legitimate CREATE2 into a false "OP-031: CREATE2 already used". This was never
    /// exercised before because no earlier test drove a real CREATE2 through the tracer;
    /// fixed by excluding CREATE/CREATE2 from that lookback re-validation.
    /// A third, smaller, already-fixed defect (see ERC7562SimulationService.
    /// SimulateWithTransactionExecutorAsync) was found and corrected alongside the main fix:
    /// every phase ran as ExecutionMode.Transaction with Sender = entryPointAddress/factory,
    /// which EIP-3607 rejects once that address has real contract code (as any live
    /// EntryPoint/factory does) — Call mode is now used instead, matching eth_call semantics.
    /// </summary>
    public class ERC7562RealAccountValidationTests
    {
        private const string EntryPointAddress = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

        private const string EntryPointDeployer = "0xd000000000000000000000000000000000000d";
        private const string FactoryDeployer = "0xd000000000000000000000000000000000000e";
        private const string TestRulesAccountDeployer = "0xd000000000000000000000000000000000000f";
        private const string TestRulesFactoryDeployer = "0xd0000000000000000000000000000000000010";
        private const string StateContractDeployer = "0xd0000000000000000000000000000000000011";
        private const string TestRulesPaymasterDeployer = "0xd0000000000000000000000000000000000012";
        private static readonly BigInteger ChainId = 1;

        private readonly InMemoryNodeDataService _nodeDataService = new();
        private readonly ERC7562SimulationService _simulationService;
        private readonly TransactionExecutor _genesisExecutor = new(DefaultHardforkConfigs.Osaka);

        public ERC7562RealAccountValidationTests()
        {
            _simulationService = new ERC7562SimulationService(_nodeDataService, DefaultHardforkConfigs.Osaka);
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        public async Task Given_RealDeployedSimpleAccount_WithCleanSignature_When_ValidationSimulated_Then_IsValidAndTraceNonEmpty()
        {
            var ownerKey = EthECKey.GenerateKey();
            var owner = ownerKey.GetPublicAddress();
            var salt = BigInteger.Zero;

            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var factoryAddress = await DeployRealSimpleAccountFactoryAsync(entryPointAddress);
            var senderCreatorAddress = await QueryEntryPointSenderCreatorAsync(entryPointAddress);
            var accountAddress = await DeployRealSimpleAccountViaFactoryAsync(factoryAddress, senderCreatorAddress, owner, salt);

            var userOp = new PackedUserOperationDTO
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };
            SignUserOp(userOp, ownerKey, entryPointAddress);

            var sender = Erc4337Entity.CreateSender(accountAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                entryPointAddress,
                sender,
                chainId: ChainId);

            Assert.True(result.IsValid,
                $"owner={owner} account={accountAddress} entryPoint={entryPointAddress} factory={factoryAddress}. " +
                "Expected the real, correctly-signed SimpleAccount op to validate. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.True(result.OpcodeExecutions.Count > 0,
                "Expected a non-empty opcode trace from the account's real validateUserOp execution " +
                "— an empty trace means the simulator is still feeding the account synthetic/insufficient calldata.");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-011")]
        public async Task Given_AccountThatOnlyTouchesTIMESTAMPWithRealCalldata_When_ValidationSimulated_Then_DetectsOP011Violation()
        {
            var senderAddress = "0x1234567890123456789012345678901234567890";
            var contractCode = BuildCalldataGatedTimestampBytecode();

            await _nodeDataService.SetCodeAsync(senderAddress, contractCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender,
                chainId: ChainId);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-011");
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-020")]
        public async Task Given_ValidationSubCallRunsOutOfGas_When_ValidationSimulated_Then_DetectsOP020Violation()
        {
            var loopContractAddress = "0x0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a";
            await _nodeDataService.SetCodeAsync(loopContractAddress, BuildInfiniteLoopBytecode());

            var senderAddress = "0x4444444444444444444444444444444444444444";
            var contractCode = BuildOutOfGasSubCallBytecode(loopContractAddress, gasStipend: 10000);
            await _nodeDataService.SetCodeAsync(senderAddress, contractCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender,
                chainId: ChainId);

            Assert.False(result.IsValid,
                "Expected the sub-frame out-of-gas to be rejected as OP-020. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "OP-020");
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-020")]
        public async Task Given_NormalValidationWithNoSubCallOutOfGas_When_ValidationSimulated_Then_NoOP020Violation()
        {
            var senderAddress = "0x5555555555555555555555555555555555555555";
            var contractCode = BuildAlwaysValidBytecode();
            await _nodeDataService.SetCodeAsync(senderAddress, contractCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender,
                chainId: ChainId);

            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-020");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-010")]
        public async Task Given_SenderCallsEntryPointDepositToDuringValidation_When_ValidationSimulated_Then_NoSTO010Violation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = "0x2222222222222222222222222222222222222222";
            var depositToSelector = new DepositToFunction { Account = senderAddress }
                .GetCallData().Take(4).ToArray();
            var contractCode = BuildEntryPointAddressArgCallBytecode(entryPointAddress, depositToSelector);
            await _nodeDataService.SetCodeAsync(senderAddress, contractCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                entryPointAddress,
                sender,
                chainId: ChainId);

            // THEN: an EIP-4337-legitimate depositTo call must not be rejected as an
            // "STO-010: Direct EntryPoint storage access not allowed (Sender@<entryPoint>)".
            Assert.True(result.IsValid,
                $"sender={senderAddress} entryPoint={entryPointAddress}. Expected the legitimate " +
                "depositTo(...) call during validateUserOp to validate without a storage violation. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.DoesNotContain(result.Violations, v => v.Rule == "STO-010");
            Assert.True(result.OpcodeExecutions.Count > 0);
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-052")]
        public async Task Given_RealTestRulesAccountDoesEthValueTransferEntryPointDepositTo_When_ValidationSimulated_Then_NoViolation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = await DeployRealTestRulesAccountAsync(entryPointAddress);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = System.Text.Encoding.ASCII.GetBytes("eth_value_transfer_entryPoint_depositTo")
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                entryPointAddress,
                sender,
                chainId: ChainId);

            Assert.True(result.IsValid,
                $"sender={senderAddress} entryPoint={entryPointAddress}. Expected the real " +
                "eth_value_transfer_entryPoint_depositTo scenario to validate without a " +
                "violation (OP-052 explicitly allows value-transfer depositTo). Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address} slot={v.Slot})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "EREP-060")]
        public async Task Given_StakedFactoryAndRealTestRulesAccountDoesTopLevelCREATE2_When_ValidationSimulated_Then_NoViolation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = await DeployRealTestRulesAccountAsync(entryPointAddress);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = System.Text.Encoding.ASCII.GetBytes("CREATE2")
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var factory = Erc4337Entity.CreateFactory("0x1234567890123456789012345678901234567890", isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.True(result.IsValid,
                $"sender={senderAddress} entryPoint={entryPointAddress}. Expected a staked " +
                "factory's account to be allowed a single top-level CREATE2 (EREP-060). " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "EREP-060")]
        public async Task Given_StakedFactoryAndRealTestRulesAccountDoesNestedCREATE_When_ValidationSimulated_Then_DetectsViolation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = await DeployRealTestRulesAccountAsync(entryPointAddress);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = System.Text.Encoding.ASCII.GetBytes("nested-CREATE")
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var factory = Erc4337Entity.CreateFactory("0x1234567890123456789012345678901234567890", isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.False(result.IsValid,
                $"sender={senderAddress} entryPoint={entryPointAddress}. Expected a nested " +
                "CREATE performed by a helper contract (not the sender/factory) to be rejected " +
                "even under a staked factory (EREP-060).");
            Assert.Contains(result.Violations, v => v.Opcode == Instruction.CREATE);
        }

        /// <summary>
        /// Builds the real, foreign-judge-owned bundler-spec-tests deployment scenario behind
        /// STO-022's "account_reference_storage_init_code" case: an unstaked TestRulesFactory
        /// CREATE2-deploys a plain SimpleWallet as the counterfactual sender (bundler-spec-
        /// tests' with_initcode() helper: factoryData = factory.create(nonce, "", entryPoint) —
        /// the rule string is carried in userOp.signature/callData instead, since
        /// TestRulesFactory.create() ignores its own rule parameter here). The deployed
        /// SimpleWallet's validateUserOp then does `State(bytes20(userOp.callData)).getState
        /// (address(this))` — sender-associated storage in a DIFFERENT (external) contract,
        /// touched during the SAME userOp that deploys the sender — ERC-7562's STO-021/022
        /// shape. Shared by both the unstaked (reject) and staked-factory (allow) tests below;
        /// only the returned Erc4337Entity's IsStaked flag differs between callers.
        /// </summary>
        private async Task<(string entryPointAddress, string factoryAddress, string senderAddress, PackedUserOperationDTO userOp)>
            BuildAccountReferenceStorageInitCodeScenarioAsync()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var factoryAddress = await DeployRealTestRulesFactoryAsync(entryPointAddress);
            var stateAddress = await DeployRealStateContractAsync();

            var nonce = BigInteger.One;
            var predictedSender = await QueryTestRulesFactoryPredictedAddressAsync(factoryAddress, nonce, entryPointAddress);
            await _nodeDataService.SetBalanceAsync(predictedSender, BigInteger.Parse("1000000000000000000"));

            var createCalldata = BuildTestRulesFactoryCreateCalldata(nonce, "", entryPointAddress);
            var initCode = factoryAddress.HexToByteArray().Concat(createCalldata).ToArray();

            var callData = new byte[20];
            stateAddress.RemoveHexPrefix().HexToByteArray().CopyTo(callData, 0);

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = initCode,
                CallData = callData,
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            return (entryPointAddress, factoryAddress, predictedSender, userOp);
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-022")]
        public async Task Given_UnstakedFactoryDeploysSenderThatReadsSenderAssociatedExternalStorage_When_ValidationSimulated_Then_DetectsViolation()
        {
            var (entryPointAddress, factoryAddress, senderAddress, userOp) = await BuildAccountReferenceStorageInitCodeScenarioAsync();

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(factoryAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.False(result.IsValid,
                $"sender={senderAddress} factory={factoryAddress} (unstaked). Expected STO-022 " +
                "to reject sender-associated external-storage access reached during an unstaked " +
                "factory's deployment of this userOp's sender.");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-022")]
        public async Task Given_StakedFactoryDeploysSenderThatReadsSenderAssociatedExternalStorage_When_ValidationSimulated_Then_NoViolation()
        {
            var (entryPointAddress, factoryAddress, senderAddress, userOp) = await BuildAccountReferenceStorageInitCodeScenarioAsync();

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(factoryAddress, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.True(result.IsValid,
                $"sender={senderAddress} factory={factoryAddress} (staked). Expected a STAKED " +
                "factory's deployment to still allow sender-associated external-storage access " +
                "(STO-022 exemption). Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address} slot={v.Slot})")));
        }

        private async Task<(string entryPointAddress, string factoryAddress, string paymasterAddress, PackedUserOperationDTO userOp)>
            BuildPaymasterAccountReferenceStorageInitCodeScenarioAsync()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var factoryAddress = await DeployRealTestRulesFactoryAsync(entryPointAddress);
            var paymasterAddress = await DeployRealTestRulesPaymasterAsync(entryPointAddress);
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));

            var nonce = BigInteger.One;
            var predictedSender = await QueryTestRulesFactoryPredictedAddressAsync(factoryAddress, nonce, entryPointAddress);
            await _nodeDataService.SetBalanceAsync(predictedSender, BigInteger.Parse("1000000000000000000"));

            var createCalldata = BuildTestRulesFactoryCreateCalldata(nonce, "", entryPointAddress);
            var initCode = factoryAddress.HexToByteArray().Concat(createCalldata).ToArray();

            var paymasterAndData = paymasterAddress.RemoveHexPrefix().HexToByteArray()
                .Concat(new byte[16])
                .Concat(new byte[16])
                .Concat(System.Text.Encoding.ASCII.GetBytes("account_reference_storage_init_code"))
                .ToArray();

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAndData,
                Signature = new byte[65]
            };

            return (entryPointAddress, factoryAddress, paymasterAddress, userOp);
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-022")]
        public async Task Given_UnstakedPaymasterReadsSenderBalanceWhileUnstakedFactoryDeploysSender_When_ValidationSimulated_Then_DetectsViolation()
        {
            var (entryPointAddress, factoryAddress, paymasterAddress, userOp) = await BuildPaymasterAccountReferenceStorageInitCodeScenarioAsync();

            var sender = Erc4337Entity.CreateSender(userOp.Sender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(factoryAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, factory: factory, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} factory={factoryAddress} (unstaked). Expected " +
                "STO-022 to reject the paymaster reading the sender's balance while an " +
                "unstaked factory deploys this userOp's sender.");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-022")]
        public async Task Given_UnstakedPaymasterReadsSenderBalanceWhileStakedFactoryDeploysSender_When_ValidationSimulated_Then_NoViolation()
        {
            var (entryPointAddress, factoryAddress, paymasterAddress, userOp) = await BuildPaymasterAccountReferenceStorageInitCodeScenarioAsync();

            var sender = Erc4337Entity.CreateSender(userOp.Sender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(factoryAddress, isStaked: true);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, factory: factory, paymaster: paymaster, chainId: ChainId);

            Assert.True(result.IsValid,
                $"paymaster={paymasterAddress} factory={factoryAddress} (staked). Expected a " +
                "STAKED factory's deployment to still allow the paymaster to read the sender's " +
                "balance (STO-022 exemption). Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address} slot={v.Slot})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-010")]
        public async Task Given_SenderCallsUnauthorizedEntryPointMethodThatTouchesStorage_When_ValidationSimulated_Then_StillDetectsSTO010()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = "0x3333333333333333333333333333333333333333";
            var getDepositInfoSelector = new GetDepositInfoFunction { Account = senderAddress }
                .GetCallData().Take(4).ToArray();
            var contractCode = BuildEntryPointAddressArgCallBytecode(entryPointAddress, getDepositInfoSelector);
            await _nodeDataService.SetCodeAsync(senderAddress, contractCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                entryPointAddress,
                sender,
                chainId: ChainId);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "OP-055");
            Assert.Contains(result.Violations, v => v.Rule == "STO-010");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        public async Task Given_CounterfactualInitCode_WithRealFactoryAndSenderCreator_When_ValidationSimulated_Then_DeploysAndValidatesWithoutOP041()
        {
            var ownerKey = EthECKey.GenerateKey();
            var owner = ownerKey.GetPublicAddress();
            var salt = BigInteger.Zero;

            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var factoryAddress = await DeployRealSimpleAccountFactoryAsync(entryPointAddress);
            var predictedSenderAddress = await QueryFactoryPredictedAddressAsync(factoryAddress, owner, salt);

            var createAccountFunction = new CreateAccountFunction { Owner = owner, Salt = salt };
            var initCode = factoryAddress.HexToByteArray()
                .Concat(createAccountFunction.GetCallData())
                .ToArray();

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSenderAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };
            SignUserOp(userOp, ownerKey, entryPointAddress);

            var sender = Erc4337Entity.CreateSender(predictedSenderAddress, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(factoryAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                entryPointAddress,
                sender,
                factory: factory,
                chainId: ChainId);

            Assert.True(result.IsValid,
                $"owner={owner} predictedSender={predictedSenderAddress} entryPoint={entryPointAddress} factory={factoryAddress}. " +
                "Expected the counterfactual deployment + validation to succeed. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.True(result.OpcodeExecutions.Count > 0,
                "Expected a non-empty opcode trace covering both the factory's CREATE2 deployment " +
                "and the deployed account's validateUserOp execution.");
            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-041");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-041")]
        public async Task Given_FactoryCallWithOversizedCallArgs_When_SenderNeverActuallyDeployed_Then_StillDetectsOP041ForUndeployedSender()
        {
            var factoryAddress = "0x4444444444444444444444444444444444444444";
            var undeployedCallTarget = "0x6666666666666666666666666666666666666666";
            var senderAddress = "0x5555555555555555555555555555555555555555";

            var factoryCode = BuildOversizedCallArgsBytecode(undeployedCallTarget);
            await _nodeDataService.SetCodeAsync(factoryAddress, factoryCode);
            await _nodeDataService.SetBalanceAsync(factoryAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var initCode = factoryAddress.HexToByteArray();

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(factoryAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender,
                factory: factory,
                chainId: ChainId);

            Assert.False(result.IsValid,
                $"factory={factoryAddress} sender={senderAddress}. Expected the still-undeployed sender " +
                "to be rejected as OP-041. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "OP-041");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "B2-T47")]
        public async Task Given_NodeDataServiceFaultsDuringSenderValidation_When_ValidationSimulated_Then_FailsClosed()
        {
            var senderAddress = "0xa0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0a0";
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var faultingReader = new FaultInjectingStateReader(_nodeDataService);
            var faultingSimulationService = new ERC7562SimulationService(faultingReader, DefaultHardforkConfigs.Osaka);

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await faultingSimulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, chainId: ChainId);

            Assert.False(result.IsValid,
                $"sender={senderAddress}. Expected a genuine mid-simulation fault to reject the op " +
                "(fail closed), not silently accept it. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "SIMULATION_ERROR");
        }

        private class FaultInjectingStateReader : IStateReader
        {
            private readonly IStateReader _inner;

            public FaultInjectingStateReader(IStateReader inner)
            {
                _inner = inner;
            }

            public Task<byte[]> GetCodeAsync(string address) => _inner.GetCodeAsync(address);
            public Task<byte[]> GetCodeAsync(byte[] address) => _inner.GetCodeAsync(address);

            public Task<EvmUInt256> GetBalanceAsync(string address) =>
                throw new InvalidOperationException("Simulated node-data-service fault (B2-T47 fail-closed test)");
            public Task<EvmUInt256> GetBalanceAsync(byte[] address) =>
                throw new InvalidOperationException("Simulated node-data-service fault (B2-T47 fail-closed test)");

            public Task<byte[]> GetStorageAtAsync(string address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);
            public Task<byte[]> GetStorageAtAsync(byte[] address, EvmUInt256 position) => _inner.GetStorageAtAsync(address, position);

            public Task<EvmUInt256> GetTransactionCountAsync(string address) => _inner.GetTransactionCountAsync(address);
            public Task<EvmUInt256> GetTransactionCountAsync(byte[] address) => _inner.GetTransactionCountAsync(address);

            public Task<byte[]> GetBlockHashAsync(long blockNumber) => _inner.GetBlockHashAsync(blockNumber);
            public Task<bool> AccountExistsAsync(string address) => _inner.AccountExistsAsync(address);
        }

        // --- ERC-7562 OP-041 (T46 traceability) ----------------------------
        // called via factoryData/initCode. ERC-7562's OP-041
        // Both gaps mean OP-041 can never fire for "access an undeployed contract",
        // reproducing the compliance suite's "'Ok' object has no attribute 'message'"
        [Theory]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-041")]
        [InlineData(Instruction.EXTCODESIZE)]
        [InlineData(Instruction.EXTCODEHASH)]
        [InlineData(Instruction.EXTCODECOPY)]
        [InlineData(Instruction.CALL)]
        [InlineData(Instruction.CALLCODE)]
        [InlineData(Instruction.DELEGATECALL)]
        [InlineData(Instruction.STATICCALL)]
        public async Task Given_FactoryOpcodeTouchesUndeployedContract_When_ValidationSimulated_Then_DetectsOP041(
            Instruction accessOpcode)
        {
            var createInitCode = BuildTrivialAccountCreate2InitCode();
            var predictedSender = ComputeCreate2Address(FactoryAddressUnderTest, createInitCode);
            var factoryCode = BuildFactoryBytecode(accessOpcode, UndeployedMagicAddress, createInitCode);

            await _nodeDataService.SetCodeAsync(FactoryAddressUnderTest, factoryCode);
            await _nodeDataService.SetBalanceAsync(FactoryAddressUnderTest, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = FactoryAddressUnderTest.HexToByteArray(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(predictedSender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(FactoryAddressUnderTest, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.False(result.IsValid,
                $"opcode={accessOpcode} target={UndeployedMagicAddress} factory={FactoryAddressUnderTest}. " +
                "Expected access to an undeployed contract to be rejected as OP-041. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "OP-041");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-042")]
        public async Task Given_FactoryTouchesOwnCounterfactualSenderBeforeDeploying_When_ValidationSimulated_Then_OP042ExemptsFactoryAccess()
        {
            var createInitCode = BuildTrivialAccountCreate2InitCode();
            var predictedSender = ComputeCreate2Address(FactoryAddressUnderTest, createInitCode);

            var bytes = new List<byte>();
            foreach (var opcode in new[]
                     {
                         Instruction.CALL, Instruction.CALLCODE, Instruction.DELEGATECALL, Instruction.STATICCALL,
                         Instruction.EXTCODESIZE, Instruction.EXTCODEHASH, Instruction.EXTCODECOPY
                     })
            {
                AppendCodeAccessOps(bytes, opcode, predictedSender);
            }
            bytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });
            var factoryCode = bytes.ToArray();

            await _nodeDataService.SetCodeAsync(FactoryAddressUnderTest, factoryCode);
            await _nodeDataService.SetBalanceAsync(FactoryAddressUnderTest, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = FactoryAddressUnderTest.HexToByteArray(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(predictedSender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(FactoryAddressUnderTest, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-041" && v.Entity == EntityType.Factory);
            Assert.Contains(result.Violations, v => v.Rule == "OP-041" && v.Entity == EntityType.Sender);
        }

        [Theory]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-054")]
        [InlineData(Instruction.EXTCODESIZE)]
        [InlineData(Instruction.EXTCODEHASH)]
        [InlineData(Instruction.EXTCODECOPY)]
        public async Task Given_SenderOpcodeTouchesEntryPointCode_When_ValidationSimulated_Then_DetectsOP054(
            Instruction accessOpcode)
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var senderCode = BuildEntryPointCodeAccessBytecode(accessOpcode, entryPointAddress);
            await _nodeDataService.SetCodeAsync(senderAddress, senderCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, chainId: ChainId);

            Assert.False(result.IsValid,
                $"opcode={accessOpcode} entryPoint={entryPointAddress} sender={senderAddress}. " +
                "Expected EXTCODE access to the EntryPoint's own code to be rejected as OP-054. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "OP-054");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "OP-054")]
        public async Task Given_SenderOpcodeTouchesUnrelatedDeployedContract_When_ValidationSimulated_Then_NotRejectedAsOP054()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var otherContractAddress = "0x9999999999999999999999999999999999999999";
            await _nodeDataService.SetCodeAsync(otherContractAddress, TrivialAccountRuntimeBytecode);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var senderCode = BuildEntryPointCodeAccessBytecode(Instruction.EXTCODESIZE, otherContractAddress);
            await _nodeDataService.SetCodeAsync(senderAddress, senderCode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, chainId: ChainId);

            Assert.True(result.IsValid,
                $"entryPoint={entryPointAddress} sender={senderAddress} other={otherContractAddress}. " +
                "Expected EXTCODESIZE against an unrelated, deployed, non-EntryPoint contract to be " +
                "allowed. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.DoesNotContain(result.Violations, v => v.Rule == "OP-054");
        }

        // --- ERC-7562 STO-031/032 (T46 traceability, B2-T41b) --------------
        // "factory_reference_storage" (STAKED factory, assert_ok — see the "staked factory"
        // contract. ERC-7562 STO-031/032 grants this ONLY to a STAKED entity; an unstaked one
        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public async Task Given_StakedFactoryWritesMappingKeyedByItsOwnAddress_When_ValidationSimulated_Then_NoSTO032Violation()
        {
            var createInitCode = BuildTrivialAccountCreate2InitCode();
            var predictedSender = ComputeCreate2Address(FactoryAddressUnderTest, createInitCode);

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildMappingMintBytecode());
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var bytes = new List<byte>();
            AppendCallCoinMintWithAddress(bytes, CoinContractAddressUnderTest, FactoryAddressUnderTest);
            AppendCreate2DeploymentTail(bytes, createInitCode);

            await _nodeDataService.SetCodeAsync(FactoryAddressUnderTest, bytes.ToArray());
            await _nodeDataService.SetBalanceAsync(FactoryAddressUnderTest, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = FactoryAddressUnderTest.HexToByteArray(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(predictedSender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(FactoryAddressUnderTest, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.True(result.IsValid,
                $"factory={FactoryAddressUnderTest} coin={CoinContractAddressUnderTest}. Expected a staked " +
                "factory's write to its own associated storage in an external contract to be allowed. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.DoesNotContain(result.Violations, v => v.Rule == "STO-032");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public async Task Given_UnstakedPaymasterWritesMappingKeyedByItsOwnAddress_When_ValidationSimulated_Then_StillDetectsSTO032()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildMappingMintBytecode());
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999a";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, paymasterAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest}. Expected an unstaked " +
                "paymaster's write to its own associated storage to still be rejected as STO-032. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "STO-032");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public void Given_UnstakedNonSenderEntityComputesKeccakOfSenderAddress_When_NoGenuineStorageAccessOccurs_Then_CannotForgeSenderAssociatedSlot()
        {
            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999a";
            var attackerChosenBaseSlot = BigInteger.Parse("123456789");

            var context = ERC7562ValidationContext.Create(
                EntryPointAddress,
                Erc4337Entity.CreateSender(senderAddress, isStaked: false),
                paymaster: Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false));
            context.CurrentEntity = EntityType.Paymaster;
            context.IsDeploymentPhase = false;

            var addressWord = new byte[32];
            senderAddress.HexToByteArray().CopyTo(addressWord, 12);
            var slotBytes = attackerChosenBaseSlot.ToByteArray(true, true);
            var slotWord = new byte[32];
            Array.Copy(slotBytes, 0, slotWord, 32 - slotBytes.Length, slotBytes.Length);
            var preimage = addressWord.Concat(slotWord).ToArray();
            var output = new Sha3Keccack().CalculateHash(preimage);
            var resultHash = new BigInteger(output, true, true);

            var associatedStorage = new AssociatedStorageCalculator();
            associatedStorage.TrackKeccak(preimage, output);

            Assert.False(context.IsAssociatedSlot(senderAddress, resultHash),
                "A bare keccak256(sender++X) computation, with no genuine storage access, " +
                "must never forge context.AssociatedSlots[sender].");

            var enforcer = new ERC7562RuleEnforcer();
            var violation = enforcer.ValidateStorageAccess(senderAddress, resultHash, isWrite: true, isTransient: false, context);

            Assert.NotNull(violation);
            Assert.Equal("STO-032", violation!.Rule);
        }

        // --- ERC-7562 STO-032 sender-association laundering into an entity's OWN contract
        // for an UNSTAKED entity via its "or not in deployment phase" clause, with no check
        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public async Task Given_UnstakedPaymasterSelfStoresAtSlotKeyedByKeccakOfSenderAddress_When_ValidationSimulated_Then_DetectsSTO032()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999b";
            var attackerChosenBaseSlot = BigInteger.Parse("424242");

            var paymasterBytes = new List<byte>();
            AppendSelfSstoreMappingKeyedByAddress(paymasterBytes, senderAddress, attackerChosenBaseSlot, storedValue: BigInteger.One);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} sender={senderAddress}. Expected an unstaked " +
                "paymaster's self-SSTORE at keccak(sender, X) in its OWN contract to be rejected " +
                "as STO-032. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "STO-032");
        }

        // --- ERC-7562 STO-031 unstaked entity reads its OWN storage (bare SLOAD, no CALL) ---
        // "Entity accessing its own storage" branch).
        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-031")]
        public async Task Given_UnstakedPaymasterReadsOwnStorage_When_ValidationSimulated_Then_DetectsSTO031Violation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999e";

            var paymasterBytes = new byte[]
            {
                0x60, 0x00,
                0x54,
                0x50,
                0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3
            };

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes);
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress}. Expected an unstaked paymaster's bare SLOAD of its own " +
                "storage to be rejected as STO-031. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "STO-031");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-031")]
        public async Task Given_UnstakedPaymasterReadsExternalNonAssociatedStorage_When_ValidationSimulated_Then_DetectsSTO031Violation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            const string unrelatedAddress = "0xdeadcafedeadcafedeadcafedeadcafedeadcafe";

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildMappingReadBytecode());
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999c";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, unrelatedAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest}. Expected an unstaked " +
                "paymaster's read of an unrelated external storage slot to be rejected as STO-031. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "STO-031");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-033")]
        public async Task Given_StakedPaymasterReadsExternalNonAssociatedStorage_When_ValidationSimulated_Then_NoViolation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            const string unrelatedAddress = "0xdeadcafedeadcafedeadcafedeadcafedeadcafe";

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildMappingReadBytecode());
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999d";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, unrelatedAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.True(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest}. Expected a staked " +
                "paymaster's read of an external storage slot to be allowed (STO-033). Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        // --- ERC-7562 EREP-050 unstaked paymaster returns a context --------------------------
        // dispatching into ValidationRules.runRule — it just returns ("this is a context", 0)
        private async Task<(string entryPointAddress, string paymasterAddress, PackedUserOperationDTO userOp)>
            BuildPaymasterReturnsContextScenarioAsync()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            var paymasterAddress = await DeployRealTestRulesPaymasterAsync(entryPointAddress);
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));

            var senderAddress = "0x8888888888888888888888888888888888888888";
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var paymasterAndData = paymasterAddress.RemoveHexPrefix().HexToByteArray()
                .Concat(new byte[16])
                .Concat(new byte[16])
                .Concat(System.Text.Encoding.ASCII.GetBytes("context"))
                .ToArray();

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAndData,
                Signature = Array.Empty<byte>()
            };

            return (entryPointAddress, paymasterAddress, userOp);
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "EREP-050")]
        public async Task Given_UnstakedPaymasterReturnsContext_When_ValidationSimulated_Then_DetectsErep050Violation()
        {
            var (entryPointAddress, paymasterAddress, userOp) = await BuildPaymasterReturnsContextScenarioAsync();

            var sender = Erc4337Entity.CreateSender(userOp.Sender, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} (unstaked). Expected an unstaked paymaster " +
                "returning a non-empty validatePaymasterUserOp context to be rejected as " +
                "EREP-050. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "EREP-050");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "EREP-050")]
        public async Task Given_StakedPaymasterReturnsContext_When_ValidationSimulated_Then_NoViolation()
        {
            var (entryPointAddress, paymasterAddress, userOp) = await BuildPaymasterReturnsContextScenarioAsync();

            var sender = Erc4337Entity.CreateSender(userOp.Sender, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.True(result.IsValid,
                $"paymaster={paymasterAddress} (staked). Expected a staked paymaster returning " +
                "a non-empty validatePaymasterUserOp context to be allowed. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-021")]
        public async Task Given_UnstakedFactoryReadsAccountReferenceStorage_When_ValidationSimulated_Then_DetectsViolation()
        {
            var createInitCode = BuildTrivialAccountCreate2InitCode();
            var predictedSender = ComputeCreate2Address(FactoryAddressUnderTest, createInitCode);

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildMappingReadBytecode());
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var bytes = new List<byte>();
            AppendCallCoinMintWithAddress(bytes, CoinContractAddressUnderTest, predictedSender);
            AppendCreate2DeploymentTail(bytes, createInitCode);

            await _nodeDataService.SetCodeAsync(FactoryAddressUnderTest, bytes.ToArray());
            await _nodeDataService.SetBalanceAsync(FactoryAddressUnderTest, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = FactoryAddressUnderTest.HexToByteArray(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(predictedSender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(FactoryAddressUnderTest, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.False(result.IsValid,
                $"factory={FactoryAddressUnderTest} coin={CoinContractAddressUnderTest} sender={predictedSender}. " +
                "Expected an unstaked factory's read of sender-associated storage during deployment to be " +
                "rejected. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-021")]
        public async Task Given_StakedFactoryReadsAccountReferenceStorage_When_ValidationSimulated_Then_NoViolation()
        {
            var createInitCode = BuildTrivialAccountCreate2InitCode();
            var predictedSender = ComputeCreate2Address(FactoryAddressUnderTest, createInitCode);

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildMappingReadBytecode());
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var bytes = new List<byte>();
            AppendCallCoinMintWithAddress(bytes, CoinContractAddressUnderTest, predictedSender);
            AppendCreate2DeploymentTail(bytes, createInitCode);

            await _nodeDataService.SetCodeAsync(FactoryAddressUnderTest, bytes.ToArray());
            await _nodeDataService.SetBalanceAsync(FactoryAddressUnderTest, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = predictedSender,
                Nonce = 0,
                InitCode = FactoryAddressUnderTest.HexToByteArray(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(predictedSender, isStaked: false);
            var factory = Erc4337Entity.CreateFactory(FactoryAddressUnderTest, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, factory: factory, chainId: ChainId);

            Assert.True(result.IsValid,
                $"factory={FactoryAddressUnderTest} coin={CoinContractAddressUnderTest} sender={predictedSender}. " +
                "Expected a staked factory's read of sender-associated storage during deployment to be " +
                "allowed. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-010")]
        public async Task Given_UnstakedSenderReadsOwnStorage_When_ValidationSimulated_Then_NoViolation()
        {
            var senderAddress = "0x4444444444444444444444444444444444444444";
            var senderBytecode = new byte[]
            {
                0x60, 0x00,
                0x54,
                0x50,
                0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3
            };
            await _nodeDataService.SetCodeAsync(senderAddress, senderBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, EntryPointAddress, sender, chainId: ChainId);

            Assert.True(result.IsValid,
                $"sender={senderAddress}. Expected the sender's read of its own storage to be allowed. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }


        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public async Task Given_UnstakedPaymasterStructWriteToOwnAddressSlot_When_ValidationSimulated_Then_DetectsSTO032Violation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildStructMemberBytecode(isWrite: true, memberOffset: 2));
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x999999999999999999999999999999999999999f";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, paymasterAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest}. Expected an unstaked " +
                "paymaster's struct-member write keyed by its own address to still be rejected as STO-032. " +
                "Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "STO-032");
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public async Task Given_StakedPaymasterStructWriteToOwnAddressSlot_When_ValidationSimulated_Then_NoViolation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildStructMemberBytecode(isWrite: true, memberOffset: 2));
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x99999999999999999999999999999999999999a1";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, paymasterAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.True(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest}. Expected a staked " +
                "paymaster's struct-member write keyed by its own address to be allowed. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-021")]
        public async Task Given_UnstakedPaymasterReadsAccountReferenceStructMember_When_ValidationSimulated_Then_NoViolation()
        {
            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildStructMemberBytecode(isWrite: false, memberOffset: 2));
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x99999999999999999999999999999999999999a2";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, senderAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.True(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest} sender={senderAddress}. " +
                "Expected an unstaked paymaster's read of the sender's struct member (account_reference_storage_struct) " +
                "to be allowed. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
        }

        [Fact]
        [Trait("Category", "ERC7562-RealAccount")]
        [Trait("Rule", "STO-032")]
        public async Task Given_StakedPaymasterWritesJustOutsideAssociatedStorageWindow_When_ValidationSimulated_Then_DetectsSTO032Violation()
        {
            const int justOutsideWindow = 128;

            var entryPointAddress = await DeployRealEntryPointAsync();
            await _nodeDataService.SetBalanceAsync(entryPointAddress, BigInteger.Parse("1000000000000000000"));

            await _nodeDataService.SetCodeAsync(CoinContractAddressUnderTest, BuildStructMemberBytecode(isWrite: true, memberOffset: justOutsideWindow));
            await _nodeDataService.SetBalanceAsync(CoinContractAddressUnderTest, BigInteger.Zero);

            var senderAddress = "0x8888888888888888888888888888888888888888";
            var paymasterAddress = "0x99999999999999999999999999999999999999a3";

            var paymasterBytes = new List<byte>();
            AppendCallCoinMintWithAddress(paymasterBytes, CoinContractAddressUnderTest, paymasterAddress);
            paymasterBytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });

            await _nodeDataService.SetCodeAsync(paymasterAddress, paymasterBytes.ToArray());
            await _nodeDataService.SetBalanceAsync(paymasterAddress, BigInteger.Parse("1000000000000000000"));
            await _nodeDataService.SetCodeAsync(senderAddress, TrivialAccountRuntimeBytecode);
            await _nodeDataService.SetBalanceAsync(senderAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = paymasterAddress.HexToByteArray(),
                Signature = Array.Empty<byte>()
            };

            var sender = Erc4337Entity.CreateSender(senderAddress, isStaked: false);
            var paymaster = Erc4337Entity.CreatePaymaster(paymasterAddress, isStaked: true);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp, entryPointAddress, sender, paymaster: paymaster, chainId: ChainId);

            Assert.False(result.IsValid,
                $"paymaster={paymasterAddress} coin={CoinContractAddressUnderTest}. Expected a write at " +
                $"offset {justOutsideWindow} (just outside the associated-storage window) to remain rejected " +
                "even for a staked entity. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.Contains(result.Violations, v => v.Rule == "STO-032");
        }


        private async Task<string> DeployRealEntryPointAsync()
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            await _nodeDataService.SetBalanceAsync(EntryPointDeployer, BigInteger.Parse("1000000000000000000000"));

            var deploymentData = new EntryPointDeployment().GetDeploymentData();
            var ctx = NewTxContext(genesisState, EntryPointDeployer, null, deploymentData, isContractCreation: true);
            var result = await _genesisExecutor.ExecuteAsync(ctx);

            Assert.True(result.Success, $"EntryPoint deployment failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return ctx.ContractAddress;
        }

        private async Task<string> DeployRealSimpleAccountFactoryAsync(string entryPointAddress)
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            await _nodeDataService.SetBalanceAsync(FactoryDeployer, BigInteger.Parse("1000000000000000000000"));

            var deployment = new SimpleAccountFactoryDeployment { EntryPoint = entryPointAddress };
            var deploymentData = deployment.GetDeploymentData();

            var ctx = NewTxContext(genesisState, FactoryDeployer, null, deploymentData, isContractCreation: true);
            var result = await _genesisExecutor.ExecuteAsync(ctx);

            Assert.True(result.Success, $"SimpleAccountFactory deployment failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return ctx.ContractAddress;
        }

        private async Task<string> DeployRealTestRulesAccountAsync(string entryPointAddress)
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            await _nodeDataService.SetBalanceAsync(TestRulesAccountDeployer, BigInteger.Parse("1000000000000000000000"));

            var creationCode = CompiledTestRulesAccountBytecode.CreationCodeHex.HexToByteArray();
            var constructorArg = new byte[32];
            entryPointAddress.RemoveHexPrefix().HexToByteArray().CopyTo(constructorArg, 12);
            var deploymentData = creationCode.Concat(constructorArg).ToArray();

            var ctx = NewTxContext(genesisState, TestRulesAccountDeployer, null, deploymentData, isContractCreation: true);
            var result = await _genesisExecutor.ExecuteAsync(ctx);

            Assert.True(result.Success, $"TestRulesAccount deployment failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return ctx.ContractAddress;
        }

        private static string ComputeSelectorHex(string signature)
        {
            var hash = new Sha3Keccack().CalculateHash(System.Text.Encoding.ASCII.GetBytes(signature));
            return "0x" + hash.Take(4).ToArray().ToHex();
        }

        private async Task<string> DeployRealTestRulesFactoryAsync(string entryPointAddress)
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            await _nodeDataService.SetBalanceAsync(TestRulesFactoryDeployer, BigInteger.Parse("1000000000000000000000"));

            var creationCode = CompiledTestRulesFactoryBytecode.CreationCodeHex.HexToByteArray();
            var constructorArg = new byte[32];
            entryPointAddress.RemoveHexPrefix().HexToByteArray().CopyTo(constructorArg, 12);
            var deploymentData = creationCode.Concat(constructorArg).ToArray();

            var ctx = NewTxContext(genesisState, TestRulesFactoryDeployer, null, deploymentData, isContractCreation: true);
            var result = await _genesisExecutor.ExecuteAsync(ctx);

            Assert.True(result.Success, $"TestRulesFactory deployment failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return ctx.ContractAddress;
        }

        private async Task<string> DeployRealStateContractAsync()
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            await _nodeDataService.SetBalanceAsync(StateContractDeployer, BigInteger.Parse("1000000000000000000000"));

            var creationCode = CompiledStateBytecode.CreationCodeHex.HexToByteArray();
            var ctx = NewTxContext(genesisState, StateContractDeployer, null, creationCode, isContractCreation: true);
            var result = await _genesisExecutor.ExecuteAsync(ctx);

            Assert.True(result.Success, $"State deployment failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return ctx.ContractAddress;
        }

        private async Task<string> DeployRealTestRulesPaymasterAsync(string entryPointAddress)
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            await _nodeDataService.SetBalanceAsync(TestRulesPaymasterDeployer, BigInteger.Parse("1000000000000000000000"));

            var creationCode = CompiledTestRulesPaymasterBytecode.CreationCodeHex.HexToByteArray();
            var constructorArg = new byte[32];
            entryPointAddress.RemoveHexPrefix().HexToByteArray().CopyTo(constructorArg, 12);
            var deploymentData = creationCode.Concat(constructorArg).ToArray();

            var ctx = NewTxContext(genesisState, TestRulesPaymasterDeployer, null, deploymentData, isContractCreation: true);
            var result = await _genesisExecutor.ExecuteAsync(ctx);

            Assert.True(result.Success, $"TestRulesPaymaster deployment failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return ctx.ContractAddress;
        }

        private async Task<string> QueryTestRulesFactoryPredictedAddressAsync(string factoryAddress, BigInteger salt, string entryPointAddress)
        {
            var queryState = new ExecutionStateService(_nodeDataService);
            var encoder = new FunctionCallEncoder();
            var calldataHex = encoder.EncodeRequest(
                ComputeSelectorHex("getAddress(uint256,address)"),
                new[] { new Parameter("uint256", "salt", 1), new Parameter("address", "_entryPoint", 2) },
                salt, entryPointAddress);

            var ctx = NewTxContext(queryState, TestRulesFactoryDeployer, factoryAddress, calldataHex.HexToByteArray(), isContractCreation: false);
            ctx.Mode = ExecutionMode.Call;

            var result = await _genesisExecutor.ExecuteAsync(ctx);
            Assert.True(result.Success, $"TestRulesFactory.getAddress() query failed: {result.Error} {result.RevertReason}");

            return DecodeAddressFromReturnData(result.ReturnData);
        }

        private static byte[] BuildTestRulesFactoryCreateCalldata(BigInteger nonce, string rule, string entryPointAddress)
        {
            var encoder = new FunctionCallEncoder();
            var calldataHex = encoder.EncodeRequest(
                ComputeSelectorHex("create(uint256,string,address)"),
                new[]
                {
                    new Parameter("uint256", "nonce", 1),
                    new Parameter("string", "rule", 2),
                    new Parameter("address", "_entryPoint", 3)
                },
                nonce, rule, entryPointAddress);
            return calldataHex.HexToByteArray();
        }

        private async Task<string> QueryEntryPointSenderCreatorAsync(string entryPointAddress)
        {
            var queryState = new ExecutionStateService(_nodeDataService);
            var senderCreatorFunction = new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.SenderCreatorFunction();
            var ctx = NewTxContext(queryState, FactoryDeployer, entryPointAddress, senderCreatorFunction.GetCallData(), isContractCreation: false);
            ctx.Mode = ExecutionMode.Call;

            var result = await _genesisExecutor.ExecuteAsync(ctx);
            Assert.True(result.Success, $"entryPoint.senderCreator() query failed: {result.Error} {result.RevertReason}");

            return DecodeAddressFromReturnData(result.ReturnData);
        }

        private async Task<string> QueryFactoryPredictedAddressAsync(string factoryAddress, string owner, BigInteger salt)
        {
            var queryState = new ExecutionStateService(_nodeDataService);
            var getAddressFunction = new GetAddressFunction { Owner = owner, Salt = salt };
            var ctx = NewTxContext(queryState, FactoryDeployer, factoryAddress, getAddressFunction.GetCallData(), isContractCreation: false);
            ctx.Mode = ExecutionMode.Call;

            var result = await _genesisExecutor.ExecuteAsync(ctx);
            Assert.True(result.Success, $"factory.getAddress() query failed: {result.Error} {result.RevertReason}");

            return DecodeAddressFromReturnData(result.ReturnData);
        }

        private async Task<string> DeployRealSimpleAccountViaFactoryAsync(
            string factoryAddress, string senderCreatorAddress, string owner, BigInteger salt)
        {
            var genesisState = new ExecutionStateService(_nodeDataService);
            var createAccountFunction = new CreateAccountFunction { Owner = owner, Salt = salt };

            var ctx = NewTxContext(genesisState, senderCreatorAddress, factoryAddress, createAccountFunction.GetCallData(), isContractCreation: false);
            ctx.Mode = ExecutionMode.Call;

            var result = await _genesisExecutor.ExecuteAsync(ctx);
            Assert.True(result.Success, $"factory.createAccount failed: Error=[{result.Error}] Revert=[{result.RevertReason}]");

            var accountAddress = DecodeAddressFromReturnData(result.ReturnData);

            foreach (var address in genesisState.AccountsState.Keys.ToList())
            {
                CommitAccountToStateReader(genesisState, address);
            }

            return accountAddress;
        }

        private static string DecodeAddressFromReturnData(byte[] returnData)
        {
            var data = returnData ?? Array.Empty<byte>();
            var addressBytes = data.Skip(Math.Max(0, data.Length - 20)).Take(20).ToArray();
            return "0x" + addressBytes.ToHex();
        }

        private void CommitAccountToStateReader(ExecutionStateService state, EvmAddress evmAddress)
        {
            if (!state.AccountsState.TryGetValue(evmAddress, out var account)) return;
            var address = evmAddress.ToHexLower();

            if (account.Code != null && account.Code.Length > 0)
            {
                _nodeDataService.SetCodeAsync(address, account.Code).GetAwaiter().GetResult();
            }

            foreach (var kvp in account.Storage)
            {
                if (kvp.Value == null) continue;
                _nodeDataService.SetStorageAsync(address, kvp.Key.ToBigInteger(), kvp.Value).GetAwaiter().GetResult();
            }
        }

        private static void SignUserOp(PackedUserOperationDTO userOp, EthECKey ownerKey, string entryPointAddress)
        {
            var structOp = new PackedUserOperation
            {
                Sender = userOp.Sender,
                Nonce = userOp.Nonce,
                InitCode = userOp.InitCode,
                CallData = userOp.CallData,
                AccountGasLimits = userOp.AccountGasLimits,
                PreVerificationGas = userOp.PreVerificationGas,
                GasFees = userOp.GasFees,
                PaymasterAndData = userOp.PaymasterAndData,
                Signature = Array.Empty<byte>()
            };

            var userOpHash = UserOperationBuilder.HashUserOperation(structOp, entryPointAddress, ChainId);
            var signatureHex = new EthereumMessageSigner().Sign(userOpHash, ownerKey);
            userOp.Signature = signatureHex.HexToByteArray();
        }

        private static TransactionExecutionContext NewTxContext(
            ExecutionStateService state, string from, string to, byte[] data, bool isContractCreation)
        {
            return new TransactionExecutionContext
            {
                Sender = from,
                To = to,
                Data = data,
                Value = EvmUInt256.Zero,
                GasLimit = 10_000_000,
                GasPrice = 1,
                MaxFeePerGas = 1,
                MaxPriorityFeePerGas = 0,
                Nonce = 0,
                IsEip1559 = true,
                IsContractCreation = isContractCreation,
                BlockNumber = 1,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                Coinbase = "0x0000000000000000000000000000000000000000",
                BaseFee = 1,
                Difficulty = 0,
                BlockGasLimit = 30_000_000,
                ChainId = EvmUInt256BigIntegerExtensions.FromBigInteger(1),
                ExecutionState = state,
                TraceEnabled = false
            };
        }

        private static byte[] BuildCalldataGatedTimestampBytecode()
        {
            var bytes = new List<byte>();

            bytes.AddRange(new byte[] { 0x60, 0x04 });
            bytes.Add(0x36);
            bytes.Add(0x11);
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            var destPlaceholderIndex = bytes.Count - 1;
            bytes.Add(0x57);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf3);

            var dest = (byte)bytes.Count;
            bytes[destPlaceholderIndex] = dest;

            bytes.Add(0x5b);
            bytes.Add(0x42);
            bytes.Add(0x50);
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf3);

            return bytes.ToArray();
        }

        private static byte[] BuildAlwaysValidBytecode()
        {
            return new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 };
        }

        private static byte[] BuildInfiniteLoopBytecode()
        {
            return new byte[] { 0x5b, 0x60, 0x00, 0x56 };
        }

        private static byte[] BuildOutOfGasSubCallBytecode(string target, int gasStipend)
        {
            var bytes = new List<byte>();

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });

            bytes.Add(0x73);
            bytes.AddRange(target.HexToByteArray());

            bytes.Add(0x61);
            bytes.Add((byte)(gasStipend >> 8));
            bytes.Add((byte)(gasStipend & 0xFF));

            bytes.Add(0xf1);
            bytes.Add(0x50);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf3);

            return bytes.ToArray();
        }

        private static byte[] BuildOversizedCallArgsBytecode(string target)
        {
            var bytes = new List<byte>();

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x63);
            bytes.AddRange(new byte[] { 0x80, 0x00, 0x00, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });

            bytes.Add(0x73);
            bytes.AddRange(target.HexToByteArray());

            bytes.Add(0x61);
            bytes.AddRange(new byte[] { 0x27, 0x10 });

            bytes.Add(0xf1);
            bytes.Add(0x50);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf3);

            return bytes.ToArray();
        }

        private static byte[] BuildEntryPointAddressArgCallBytecode(string entryPointAddress, byte[] selector)
        {
            var bytes = new List<byte>();

            var selectorWord = new byte[32];
            selector.CopyTo(selectorWord, 0);
            bytes.Add(0x7f);
            bytes.AddRange(selectorWord);
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x04 });
            bytes.Add(0x52);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x24 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });

            bytes.Add(0x73);
            bytes.AddRange(entryPointAddress.HexToByteArray());

            bytes.Add(0x5a);
            bytes.Add(0xf1);
            bytes.Add(0x50);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf3);

            return bytes.ToArray();
        }


        private const string FactoryAddressUnderTest = "0x7777777777777777777777777777777777777777";

        private const string CoinContractAddressUnderTest = "0xc01ec01ec01ec01ec01ec01ec01ec01ec01ec01e";

        private const string UndeployedMagicAddress = "0x0000000000000000000000000000000000018894";

        private static readonly byte[] TrivialAccountRuntimeBytecode =
        {
            0x60, 0x00,
            0x60, 0x00,
            0x52,
            0x60, 0x20,
            0x60, 0x00,
            0xf3
        };

        private static byte[] BuildTrivialAccountCreate2InitCode()
        {
            var runtime = TrivialAccountRuntimeBytecode;
            var bytes = new List<byte>
            {
                0x60, (byte)runtime.Length,
                0x60, 0x0c,
                0x60, 0x00,
                0x39,
                0x60, (byte)runtime.Length,
                0x60, 0x00,
                0xf3
            };
            bytes.AddRange(runtime);
            return bytes.ToArray();
        }

        private static string ComputeCreate2Address(string deployerAddress, byte[] initCode)
        {
            var saltHex = "0x" + new string('0', 64);
            var initCodeHex = "0x" + initCode.ToHex();
            return ContractUtils.CalculateCreate2Address(deployerAddress, saltHex, initCodeHex);
        }

        private static byte[] BuildFactoryBytecode(Instruction accessOpcode, string targetAddress, byte[] create2InitCode)
        {
            var bytes = new List<byte>();
            AppendCodeAccessOps(bytes, accessOpcode, targetAddress);
            AppendCreate2DeploymentTail(bytes, create2InitCode);
            return bytes.ToArray();
        }

        private static void AppendCreate2DeploymentTail(List<byte> bytes, byte[] create2InitCode)
        {
            var len = (byte)create2InitCode.Length;

            bytes.AddRange(new byte[] { 0x60, len });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            var blobOffsetPlaceholderIndex = bytes.Count - 1;
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x39);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, len });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf5);
            bytes.Add(0x50);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0xf3);

            bytes[blobOffsetPlaceholderIndex] = (byte)bytes.Count;
            bytes.AddRange(create2InitCode);
        }

        private static void AppendCodeAccessOps(List<byte> bytes, Instruction opcode, string targetAddress)
        {
            var addressBytes = targetAddress.HexToByteArray();

            switch (opcode)
            {
                case Instruction.EXTCODESIZE:
                    bytes.Add(0x73); bytes.AddRange(addressBytes);
                    bytes.Add(0x3b);
                    bytes.Add(0x50);
                    break;

                case Instruction.EXTCODEHASH:
                    bytes.Add(0x73); bytes.AddRange(addressBytes);
                    bytes.Add(0x3f);
                    bytes.Add(0x50);
                    break;

                case Instruction.EXTCODECOPY:
                    bytes.AddRange(new byte[] { 0x60, 0x02 });
                    bytes.AddRange(new byte[] { 0x60, 0x00 });
                    bytes.AddRange(new byte[] { 0x60, 0x00 });
                    bytes.Add(0x73); bytes.AddRange(addressBytes);
                    bytes.Add(0x3c);
                    break;

                case Instruction.CALL:
                case Instruction.CALLCODE:
                case Instruction.DELEGATECALL:
                case Instruction.STATICCALL:
                    AppendCallFamilyOps(bytes, opcode, addressBytes);
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(opcode), opcode, "Unsupported access opcode for this test helper.");
            }
        }

        private static byte[] BuildEntryPointCodeAccessBytecode(Instruction accessOpcode, string targetAddress)
        {
            var bytes = new List<byte>();
            AppendCodeAccessOps(bytes, accessOpcode, targetAddress);
            bytes.AddRange(new byte[] { 0x60, 0x00, 0x60, 0x00, 0x52, 0x60, 0x20, 0x60, 0x00, 0xf3 });
            return bytes.ToArray();
        }

        private static void AppendCallFamilyOps(List<byte> bytes, Instruction opcode, byte[] addressBytes)
        {
            var hasValueOperand = opcode == Instruction.CALL || opcode == Instruction.CALLCODE;

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            if (hasValueOperand)
            {
                bytes.AddRange(new byte[] { 0x60, 0x00 });
            }
            bytes.Add(0x73); bytes.AddRange(addressBytes);
            bytes.AddRange(new byte[] { 0x61, 0x27, 0x10 });

            bytes.Add(opcode switch
            {
                Instruction.CALL => (byte)0xf1,
                Instruction.CALLCODE => (byte)0xf2,
                Instruction.DELEGATECALL => (byte)0xf4,
                Instruction.STATICCALL => (byte)0xfa,
                _ => throw new ArgumentOutOfRangeException(nameof(opcode))
            });
            bytes.Add(0x50);
        }

        private static byte[] BuildMappingMintBytecode()
        {
            return new byte[]
            {
                0x60, 0x00,
                0x35,
                0x60, 0x00,
                0x52,
                0x60, 0x00,
                0x60, 0x20,
                0x52,
                0x60, 0x40,
                0x60, 0x00,
                0x20,
                0x80,
                0x54,
                0x60, 0x64,
                0x01,
                0x90,
                0x55,
                0x00
            };
        }

        private static byte[] BuildMappingReadBytecode()
        {
            return new byte[]
            {
                0x60, 0x00,
                0x35,
                0x60, 0x00,
                0x52,
                0x60, 0x00,
                0x60, 0x20,
                0x52,
                0x60, 0x40,
                0x60, 0x00,
                0x20,
                0x54,
                0x60, 0x00,
                0x52,
                0x60, 0x20,
                0x60, 0x00,
                0xf3
            };
        }

        private static byte[] BuildStructMemberBytecode(bool isWrite, int memberOffset)
        {
            var bytes = new List<byte>
            {
                0x60, 0x00,
                0x35,
                0x60, 0x00,
                0x52,
                0x60, 0x01,
                0x60, 0x20,
                0x52,
                0x60, 0x40,
                0x60, 0x00,
                0x20
            };

            if (memberOffset != 0)
            {
                bytes.AddRange(new byte[] { 0x60, (byte)memberOffset });
                bytes.Add(0x01);
            }

            if (isWrite)
            {
                bytes.AddRange(new byte[] { 0x60, 0x03 });
                bytes.Add(0x90);
                bytes.Add(0x55);
                bytes.Add(0x00);
            }
            else
            {
                bytes.Add(0x54);
                bytes.AddRange(new byte[] { 0x60, 0x00 });
                bytes.Add(0x52);
                bytes.AddRange(new byte[] { 0x60, 0x20, 0x60, 0x00, 0xf3 });
            }

            return bytes.ToArray();
        }

        private static void AppendCallCoinMintWithAddress(List<byte> bytes, string coinAddress, string addressArgument)
        {
            var addressWord = new byte[32];
            addressArgument.HexToByteArray().CopyTo(addressWord, 12);

            bytes.Add(0x7f); bytes.AddRange(addressWord);
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);

            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x73); bytes.AddRange(coinAddress.HexToByteArray());
            bytes.AddRange(new byte[] { 0x61, 0xEA, 0x60 });
            bytes.Add(0xf1);
            bytes.Add(0x50);
        }

        private static void AppendSelfSstoreMappingKeyedByAddress(List<byte> bytes, string keyAddress, BigInteger baseSlot, BigInteger storedValue)
        {
            var addressWord = new byte[32];
            keyAddress.HexToByteArray().CopyTo(addressWord, 12);
            bytes.Add(0x7f); bytes.AddRange(addressWord);
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x52);

            var slotWord = new byte[32];
            var slotBytes = baseSlot.ToByteArray(true, true);
            Array.Copy(slotBytes, 0, slotWord, 32 - slotBytes.Length, slotBytes.Length);
            bytes.Add(0x7f); bytes.AddRange(slotWord);
            bytes.AddRange(new byte[] { 0x60, 0x20 });
            bytes.Add(0x52);

            bytes.AddRange(new byte[] { 0x60, 0x40 });
            bytes.AddRange(new byte[] { 0x60, 0x00 });
            bytes.Add(0x20);

            var valueWord = new byte[32];
            var valueBytes = storedValue.ToByteArray(true, true);
            Array.Copy(valueBytes, 0, valueWord, 32 - valueBytes.Length, valueBytes.Length);
            bytes.Add(0x7f); bytes.AddRange(valueWord);
            bytes.Add(0x90);
            bytes.Add(0x55);
        }
    }
}
