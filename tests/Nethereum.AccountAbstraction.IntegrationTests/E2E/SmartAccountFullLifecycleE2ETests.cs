using System.Numerics;
using Call = Nethereum.AccountAbstraction.BaseAccount.ContractDefinition.Call;
using Nethereum.AccountAbstraction.Builders;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory;
using Nethereum.AccountAbstraction.Contracts.Core.NethereumAccountFactory.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator;
using Nethereum.AccountAbstraction.Contracts.Modules.Native.ECDSAValidator.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.SocialRecovery.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.SudoPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.Policies.ERC20SpendingLimitPolicy.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession.ContractDefinition;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.AccountAbstraction.Extensions;
using Nethereum.AccountAbstraction.Paymasters;
using Nethereum.AccountAbstraction.Services;
using Nethereum.AccountAbstraction.SessionKeys;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.ABI;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.StandardTokenEIP20;
using Nethereum.StandardTokenEIP20.ContractDefinition;
using Nethereum.Web3;
using Xunit;
using Xunit.Abstractions;
using Web3Account = Nethereum.Web3.Accounts.Account;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    public class SmartAccountFullLifecycleE2ETests : IAsyncLifetime
    {
        private readonly ITestOutputHelper _output;

        private const int CHAIN_ID = 31337;
        private const string OWNER_PRIVATE_KEY = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private const string GUARDIAN1_PRIVATE_KEY = "0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690d";
        private const string GUARDIAN2_PRIVATE_KEY = "0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365a";

        private DevChainNode _node = null!;
        private IWeb3 _web3 = null!;
        private Web3Account _ownerAccount = null!;
        private EthECKey _ownerKey = null!;
        private EthECKey _guardian1Key = null!;
        private EthECKey _guardian2Key = null!;

        private EntryPointService _entryPointService = null!;
        private NethereumAccountFactoryService _factoryService = null!;
        private ECDSAValidatorService _ecdsaValidatorService = null!;
        private SmartSessionService _smartSessionService = null!;
        private SocialRecoveryService _socialRecoveryService = null!;
        private SudoPolicyService _sudoPolicyService = null!;
        private ERC20SpendingLimitPolicyService _spendingLimitPolicyService = null!;
        private VerifyingPaymasterService _paymasterContractService = null!;
        private StandardTokenService _tokenService = null!;

        public SmartAccountFullLifecycleE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

        public async Task InitializeAsync()
        {
            _ownerAccount = new Web3Account(OWNER_PRIVATE_KEY, CHAIN_ID);
            _ownerKey = new EthECKey(OWNER_PRIVATE_KEY);
            _guardian1Key = new EthECKey(GUARDIAN1_PRIVATE_KEY);
            _guardian2Key = new EthECKey(GUARDIAN2_PRIVATE_KEY);

            var config = new DevChainConfig
            {
                ChainId = CHAIN_ID,
                BaseFee = 1_000_000_000,
                BlockGasLimit = 30_000_000,
                AutoMine = true
            };

            _node = new DevChainNode(config);
            await _node.StartAsync(new[]
            {
                _ownerAccount.Address,
                _guardian1Key.GetPublicAddress(),
                _guardian2Key.GetPublicAddress()
            }, Web3.Web3.Convert.ToWei(10000));

            _web3 = _node.CreateWeb3(_ownerAccount);

            _output.WriteLine("Deploying infrastructure contracts...");

            _entryPointService = await EntryPointService.DeployContractAndGetServiceAsync(
                _web3, new EntryPointDeployment());
            _output.WriteLine($"  EntryPoint: {_entryPointService.ContractAddress}");

            _ecdsaValidatorService = await ECDSAValidatorService.DeployContractAndGetServiceAsync(
                _web3, new ECDSAValidatorDeployment());
            _output.WriteLine($"  ECDSAValidator: {_ecdsaValidatorService.ContractAddress}");

            _factoryService = await NethereumAccountFactoryService.DeployContractAndGetServiceAsync(
                _web3, new NethereumAccountFactoryDeployment { EntryPoint = _entryPointService.ContractAddress });
            _output.WriteLine($"  AccountFactory: {_factoryService.ContractAddress}");

            _smartSessionService = await SmartSessionService.DeployContractAndGetServiceAsync(
                _web3, new SmartSessionDeployment());
            _output.WriteLine($"  SmartSession: {_smartSessionService.ContractAddress}");

            _socialRecoveryService = await SocialRecoveryService.DeployContractAndGetServiceAsync(
                _web3, new SocialRecoveryDeployment());
            _output.WriteLine($"  SocialRecovery: {_socialRecoveryService.ContractAddress}");

            _sudoPolicyService = await SudoPolicyService.DeployContractAndGetServiceAsync(
                _web3, new SudoPolicyDeployment());
            _output.WriteLine($"  SudoPolicy: {_sudoPolicyService.ContractAddress}");

            _spendingLimitPolicyService = await ERC20SpendingLimitPolicyService.DeployContractAndGetServiceAsync(
                _web3, new ERC20SpendingLimitPolicyDeployment());
            _output.WriteLine($"  ERC20SpendingLimitPolicy: {_spendingLimitPolicyService.ContractAddress}");

            _paymasterContractService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _web3, new VerifyingPaymasterDeployment
                {
                    EntryPoint = _entryPointService.ContractAddress,
                    Owner = _ownerAccount.Address,
                    Signer = _ownerAccount.Address
                });
            _output.WriteLine($"  VerifyingPaymaster: {_paymasterContractService.ContractAddress}");

            _tokenService = await StandardTokenService.DeployContractAndGetServiceAsync(
                (Web3.Web3)_web3, new EIP20Deployment
                {
                    InitialAmount = Web3.Web3.Convert.ToWei(1_000_000),
                    TokenName = "Test Token",
                    TokenSymbol = "TEST",
                    DecimalUnits = 18
                });
            _output.WriteLine($"  TestToken: {_tokenService.ContractHandler.ContractAddress}");

            _output.WriteLine("Infrastructure deployed successfully.");
        }

        public Task DisposeAsync()
        {
            _node?.Dispose();
            return Task.CompletedTask;
        }

        [Fact]
        [Trait("Category", "E2E-FullLifecycle")]
        [Trait("Workflow", "BestPractices")]
        public async Task FullLifecycle_AccountCreation_Modules_Sessions_Recovery()
        {
            _output.WriteLine("\n========== SMART ACCOUNT FULL LIFECYCLE E2E TEST ==========\n");

            // ============================================================
            // PHASE 1: Create Smart Account using SmartAccountFactoryService
            // ============================================================
            _output.WriteLine("PHASE 1: Creating Smart Account with SmartAccountFactoryService");

            var salt = CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var initData = CreateInitData();

            var factoryService = await SmartAccountFactoryService.LoadAsync(_web3, _factoryService.ContractAddress);
            _output.WriteLine($"  Factory loaded: {factoryService.Address}");
            _output.WriteLine($"  EntryPoint: {factoryService.EntryPointAddress}");

            var predictedAddress = await factoryService.GetAccountAddressAsync(salt, initData);
            _output.WriteLine($"  Predicted account address: {predictedAddress}");

            await _node.SetBalanceAsync(predictedAddress, Web3.Web3.Convert.ToWei(10));
            _output.WriteLine($"  Funded account with 10 ETH");

            await factoryService.CreateAccountAsync(salt, initData);
            var smartAccountService = await SmartAccountService.LoadAsync(_web3, predictedAddress);
            Assert.Equal(predictedAddress, smartAccountService.Address);
            _output.WriteLine($"  Account created at: {smartAccountService.Address}");

            var isDeployed = await smartAccountService.IsDeployedAsync();
            Assert.True(isDeployed);
            _output.WriteLine($"  Account deployed: {isDeployed}");

            // ============================================================
            // PHASE 2: Query Module Support (SmartSession is TYPE_VALIDATOR)
            // ============================================================
            _output.WriteLine("\nPHASE 2: Querying SmartSession Module Support");

            var moduleTypeValidator = ERC7579ModuleTypes.TYPE_VALIDATOR;
            var isSmartSessionValidator = await _smartSessionService.IsModuleTypeQueryAsync(moduleTypeValidator);
            Assert.True(isSmartSessionValidator);
            _output.WriteLine($"  SmartSession is validator module: {isSmartSessionValidator}");

            var isSmartSessionExecutor = await _smartSessionService.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);
            Assert.False(isSmartSessionExecutor);
            _output.WriteLine($"  SmartSession is executor module: {isSmartSessionExecutor}");

            // ============================================================
            // PHASE 3: Query SocialRecovery Module (it's an executor)
            // ============================================================
            _output.WriteLine("\nPHASE 3: Querying SocialRecovery Module Support");

            var guardian1Address = _guardian1Key.GetPublicAddress();
            var guardian2Address = _guardian2Key.GetPublicAddress();

            var isSocialRecoveryValidator = await _socialRecoveryService.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_VALIDATOR);
            var isSocialRecoveryExecutor = await _socialRecoveryService.IsModuleTypeQueryAsync(ERC7579ModuleTypes.TYPE_EXECUTOR);
            _output.WriteLine($"  SocialRecovery is validator: {isSocialRecoveryValidator}");
            _output.WriteLine($"  SocialRecovery is executor: {isSocialRecoveryExecutor}");
            _output.WriteLine($"  Guardian 1 address: {guardian1Address}");
            _output.WriteLine($"  Guardian 2 address: {guardian2Address}");

            // ============================================================
            // PHASE 4: Create Session using SessionKeyManager
            // ============================================================
            _output.WriteLine("\nPHASE 4: Creating Session with SessionKeyManager");

            var sessionKeyStore = new InMemorySessionKeyStore();
            var sessionKeyManager = new SessionKeyManager(sessionKeyStore);

            var generatedSession = await sessionKeyManager.GenerateSessionKeyAsync(
                smartAccountService.Address, validDays: 7);
            _output.WriteLine($"  Session key generated: {generatedSession.Key}");
            _output.WriteLine($"  Valid until: {DateTimeOffset.FromUnixTimeSeconds((long)generatedSession.ValidUntil)}");

            var dailyLimit = Web3.Web3.Convert.ToWei(1000);
            var tokenAddress = _tokenService.ContractHandler.ContractAddress;

            var sessionConfig = new SmartSessionConfig()
                .WithSessionValidator(_ecdsaValidatorService.ContractAddress)
                .WithSessionValidatorInitData(generatedSession.Key)
                .WithSalt(CreateSalt((ulong)Random.Shared.NextInt64()))
                .WithPaymasterPermission(true)
                .WithERC20TransferAction(
                    tokenAddress,
                    _spendingLimitPolicyService.ContractAddress,
                    ERC20SpendingLimitBuilder.SingleToken(tokenAddress, dailyLimit));

            var session = sessionConfig.ToSession();
            var permissionId = await _smartSessionService.GetPermissionIdQueryAsync(session);
            _output.WriteLine($"  Permission ID: {permissionId.ToHex()}");
            _output.WriteLine($"  Daily ERC20 limit: {Web3.Web3.Convert.FromWei(dailyLimit)} TEST");

            await sessionKeyManager.MarkRegisteredAsync(generatedSession.Key);
            var storedKey = await sessionKeyManager.GetSessionKeyAsync(generatedSession.Key);
            Assert.NotNull(storedKey);
            Assert.True(storedKey.IsActive);
            _output.WriteLine($"  Session key marked as active");

            // ============================================================
            // PHASE 5: Setup VerifyingPaymasterManager
            // ============================================================
            _output.WriteLine("\nPHASE 5: Setting up VerifyingPaymasterManager");

            var paymasterManager = await VerifyingPaymasterManager.LoadAsync(
                _web3, _paymasterContractService.ContractAddress, _ownerKey);
            _output.WriteLine($"  PaymasterManager loaded: {paymasterManager.Address}");
            _output.WriteLine($"  EntryPoint: {paymasterManager.EntryPointAddress}");

            var depositAmount = Web3.Web3.Convert.ToWei(5);
            await paymasterManager.DepositAsync(depositAmount);
            _output.WriteLine($"  Deposited {Web3.Web3.Convert.FromWei(depositAmount)} ETH to EntryPoint");

            var paymasterDeposit = await paymasterManager.GetDepositAsync();
            _output.WriteLine($"  Paymaster deposit balance: {Web3.Web3.Convert.FromWei(paymasterDeposit)} ETH");
            Assert.True(paymasterDeposit > 0);

            // ============================================================
            // PHASE 6: Transfer tokens to smart account
            // ============================================================
            _output.WriteLine("\nPHASE 6: Funding Smart Account with ERC20 Tokens");

            var fundAmount = Web3.Web3.Convert.ToWei(5000);
            await _tokenService.TransferRequestAndWaitForReceiptAsync(
                new TransferFunction { To = smartAccountService.Address, Value = fundAmount });

            var accountTokenBalance = await _tokenService.BalanceOfQueryAsync(smartAccountService.Address);
            Assert.Equal(fundAmount, accountTokenBalance);
            _output.WriteLine($"  Account token balance: {Web3.Web3.Convert.FromWei(accountTokenBalance)} TEST");

            // ============================================================
            // PHASE 7: Verify ERC-7579 Execution Encoding
            // ============================================================
            // Note: Direct execution via SmartAccountService.ExecuteAsync requires
            // the call to come from the EntryPoint (via UserOp) or the account itself.
            // Here we verify the ERC-7579 encoding utilities work correctly.
            _output.WriteLine("\nPHASE 7: Verifying ERC-7579 Execution Encoding");

            var recipient = "0x" + new string('A', 40);
            var transferAmount = Web3.Web3.Convert.ToWei(100);

            var transfer = new TransferFunction { To = recipient, Value = transferAmount };
            var call = new Call
            {
                Target = tokenAddress,
                Value = 0,
                Data = transfer.GetCallData()
            };

            var singleMode = ERC7579ModeLib.EncodeSingleDefault();
            var singleCalldata = ERC7579ExecutionLib.EncodeSingle(call.Target, call.Value, call.Data);
            Assert.Equal(32, singleMode.Length);
            Assert.True(singleCalldata.Length > 0);
            _output.WriteLine($"  Single call mode: {singleMode.ToHex()}");
            _output.WriteLine($"  Single calldata length: {singleCalldata.Length} bytes");
            _output.WriteLine($"  Target: {call.Target}");
            _output.WriteLine($"  Value: {call.Value}");
            _output.WriteLine($"  Data length: {call.Data.Length} bytes");

            // ============================================================
            // PHASE 8: Verify Batch Execution Encoding
            // ============================================================
            _output.WriteLine("\nPHASE 8: Verifying ERC-7579 Batch Execution Encoding");

            var recipient2 = "0x" + new string('B', 40);
            var recipient3 = "0x" + new string('C', 40);
            var batchAmount = Web3.Web3.Convert.ToWei(50);

            var batchCalls = new Call[]
            {
                new Call
                {
                    Target = tokenAddress,
                    Value = 0,
                    Data = new TransferFunction { To = recipient2, Value = batchAmount }.GetCallData()
                },
                new Call
                {
                    Target = tokenAddress,
                    Value = 0,
                    Data = new TransferFunction { To = recipient3, Value = batchAmount }.GetCallData()
                }
            };

            var batchMode = ERC7579ModeLib.EncodeBatchDefault();
            var batchCalldata = ERC7579ExecutionLib.EncodeBatch(batchCalls);
            Assert.Equal(32, batchMode.Length);
            Assert.True(batchCalldata.Length > 0);
            _output.WriteLine($"  Batch mode: {batchMode.ToHex()}");
            _output.WriteLine($"  Batch calldata length: {batchCalldata.Length} bytes");
            _output.WriteLine($"  Number of calls in batch: {batchCalls.Length}");
            foreach (var c in batchCalls)
            {
                _output.WriteLine($"    - Target: {c.Target}, Value: {c.Value}, Data: {c.Data.Length} bytes");
            }

            // ============================================================
            // PHASE 9: Test Paymaster Sponsorship
            // ============================================================
            _output.WriteLine("\nPHASE 9: Testing Paymaster Sponsorship");

            var mockUserOp = new Nethereum.AccountAbstraction.Structs.PackedUserOperation
            {
                Sender = smartAccountService.Address,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 100000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = Array.Empty<byte>()
            };

            var sponsorResult = await paymasterManager.SponsorUserOperationAsync(mockUserOp);
            Assert.True(sponsorResult.IsSponsored);
            Assert.NotEmpty(sponsorResult.PaymasterAndData);
            _output.WriteLine($"  Sponsorship successful!");
            _output.WriteLine($"    PaymasterAndData length: {sponsorResult.PaymasterAndData.Length} bytes");
            _output.WriteLine($"    Valid until: {DateTimeOffset.FromUnixTimeSeconds((long)sponsorResult.ValidUntil)}");

            // ============================================================
            // PHASE 10: Session Key Management - Retrieve Best Key
            // ============================================================
            _output.WriteLine("\nPHASE 10: Session Key Management");

            var bestKey = await sessionKeyManager.GetBestSessionKeyAsync(smartAccountService.Address);
            Assert.NotNull(bestKey);
            Assert.True(bestKey.IsValidNow());
            _output.WriteLine($"  Best session key: {bestKey.Key}");
            _output.WriteLine($"  Is valid now: {bestKey.IsValidNow()}");

            var allKeys = await sessionKeyManager.GetSessionKeysForAccountAsync(smartAccountService.Address);
            Assert.Single(allKeys);
            _output.WriteLine($"  Total session keys for account: {allKeys.Length}");

            // ============================================================
            // PHASE 11: Revoke Session Key
            // ============================================================
            _output.WriteLine("\nPHASE 11: Revoking Session Key");

            await sessionKeyManager.RemoveSessionKeyAsync(generatedSession.Key);

            var removedKey = await sessionKeyManager.GetSessionKeyAsync(generatedSession.Key);
            Assert.Null(removedKey);
            _output.WriteLine($"  Session key removed from store");

            var noMoreKeys = await sessionKeyManager.GetSessionKeysForAccountAsync(smartAccountService.Address);
            Assert.Empty(noMoreKeys);
            _output.WriteLine($"  Session keys remaining: {noMoreKeys.Length}");

            // ============================================================
            // PHASE 12: Verify Policy Contracts
            // ============================================================
            _output.WriteLine("\nPHASE 12: Verifying Policy Contracts");

            var sudoPolicySupports = await _sudoPolicyService.SupportsInterfaceQueryAsync("0x05c00895".HexToByteArray());
            Assert.True(sudoPolicySupports);
            _output.WriteLine($"  SudoPolicy supports IActionPolicy: {sudoPolicySupports}");

            var spendingLimitSupports = await _spendingLimitPolicyService.SupportsInterfaceQueryAsync("0x05c00895".HexToByteArray());
            Assert.True(spendingLimitSupports);
            _output.WriteLine($"  ERC20SpendingLimitPolicy supports IActionPolicy: {spendingLimitSupports}");

            // ============================================================
            // PHASE 13: Verify Final Account State
            // ============================================================
            _output.WriteLine("\nPHASE 13: Final Account State Verification");

            var finalTokenBalance = await _tokenService.BalanceOfQueryAsync(smartAccountService.Address);
            Assert.Equal(fundAmount, finalTokenBalance);
            _output.WriteLine($"  Final token balance: {Web3.Web3.Convert.FromWei(finalTokenBalance)} TEST");

            var finalDeposit = await smartAccountService.GetDepositAsync();
            _output.WriteLine($"  Account deposit at EntryPoint: {Web3.Web3.Convert.FromWei(finalDeposit)} ETH");

            var nonce = await smartAccountService.GetNonceAsync(0);
            _output.WriteLine($"  Account nonce: {nonce}");

            _output.WriteLine("\n========== TEST COMPLETED SUCCESSFULLY ==========");
            _output.WriteLine("\nConsolidated services used in this test (from Nethereum.AccountAbstraction):");
            _output.WriteLine("  FROM Services/:");
            _output.WriteLine("    - SmartAccountService - high-level account wrapper");
            _output.WriteLine("    - SmartAccountFactoryService - factory wrapper for account creation");
            _output.WriteLine("    - ISmartAccount, ISmartAccountFactory - service interfaces");
            _output.WriteLine("  FROM SessionKeys/:");
            _output.WriteLine("    - SessionKeyManager - client-side session key management");
            _output.WriteLine("    - InMemorySessionKeyStore - in-memory key storage");
            _output.WriteLine("    - ISessionKeyStore - storage abstraction interface");
            _output.WriteLine("    - SessionKeyEntry, GeneratedSessionKey - DTOs");
            _output.WriteLine("  FROM Paymasters/:");
            _output.WriteLine("    - VerifyingPaymasterManager - paymaster signature handling");
            _output.WriteLine("    - IPaymasterManager, IVerifyingPaymasterManager - interfaces");
            _output.WriteLine("    - SponsorResult, SponsorContext - DTOs");
            _output.WriteLine("  FROM ERC7579/Modules/SmartSession/:");
            _output.WriteLine("    - SmartSessionConfig - fluent session configuration");
            _output.WriteLine("    - ERC20SpendingLimitBuilder - spending limit encoding");
            _output.WriteLine("    - ActionDataBuilder - action configuration");
            _output.WriteLine("  FROM Extensions/:");
            _output.WriteLine("    - Web3SmartAccountExtensions - IWeb3 extension methods");
        }

        [Fact]
        [Trait("Category", "E2E-FullLifecycle")]
        [Trait("Workflow", "Web3Extensions")]
        public async Task Web3Extensions_CreateAndManageAccounts()
        {
            _output.WriteLine("\n========== WEB3 EXTENSIONS E2E TEST ==========\n");

            // ============================================================
            // Using Web3 extension methods for cleaner API
            // ============================================================
            _output.WriteLine("PHASE 1: Create Account via Factory Extension");

            var salt = CreateSalt((ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());
            var initData = CreateInitData();

            var factoryService = await _web3.GetSmartAccountFactoryAsync(_factoryService.ContractAddress);
            var address = await factoryService.GetAccountAddressAsync(salt, initData);
            await _node.SetBalanceAsync(address, Web3.Web3.Convert.ToWei(5));

            await factoryService.CreateAccountAsync(salt, initData);
            var account = await _web3.GetSmartAccountAsync(address);
            _output.WriteLine($"  Account created: {account.Address}");

            // ============================================================
            // Load existing account via extension
            // ============================================================
            _output.WriteLine("\nPHASE 2: Load Existing Account via Extension");

            var loadedAccount = await _web3.GetSmartAccountAsync(account.Address);
            Assert.Equal(account.Address, loadedAccount.Address);
            _output.WriteLine($"  Loaded account: {loadedAccount.Address}");

            // ============================================================
            // Load factory via extension
            // ============================================================
            _output.WriteLine("\nPHASE 3: Load Factory via Extension");

            var factory = await _web3.GetSmartAccountFactoryAsync(_factoryService.ContractAddress);
            Assert.Equal(_factoryService.ContractAddress, factory.Address);
            _output.WriteLine($"  Factory loaded: {factory.Address}");

            // ============================================================
            // Load paymaster via extension
            // ============================================================
            _output.WriteLine("\nPHASE 4: Load Paymaster via Extension");

            await _node.SetBalanceAsync(_paymasterContractService.ContractAddress, Web3.Web3.Convert.ToWei(10));
            await _paymasterContractService.DepositRequestAndWaitForReceiptAsync();

            var paymaster = await _web3.GetVerifyingPaymasterAsync(
                _paymasterContractService.ContractAddress, _ownerKey);
            Assert.Equal(_paymasterContractService.ContractAddress, paymaster.Address);
            _output.WriteLine($"  Paymaster loaded: {paymaster.Address}");

            // ============================================================
            // Deposit paymaster via extension
            // ============================================================
            _output.WriteLine("\nPHASE 5: Load Deposit Paymaster via Extension");

            var depositPaymaster = await _web3.GetDepositPaymasterAsync(
                _paymasterContractService.ContractAddress);
            _output.WriteLine($"  Deposit paymaster loaded: {depositPaymaster.Address}");

            _output.WriteLine("\n========== WEB3 EXTENSIONS TEST COMPLETED ==========");
        }

        [Fact]
        [Trait("Category", "E2E-FullLifecycle")]
        [Trait("Workflow", "SessionKeyStore")]
        public async Task SessionKeyStore_PersistenceAndRetrieval()
        {
            _output.WriteLine("\n========== SESSION KEY STORE E2E TEST ==========\n");

            var accountAddress = "0x" + new string('1', 40);

            // ============================================================
            // Test InMemorySessionKeyStore
            // ============================================================
            _output.WriteLine("PHASE 1: InMemorySessionKeyStore Operations");

            var store = new InMemorySessionKeyStore();
            var manager = new SessionKeyManager(store);

            var key1 = await manager.GenerateSessionKeyAsync(accountAddress, validDays: 30);
            var key2 = await manager.GenerateSessionKeyAsync(accountAddress, validDays: 7);
            var key3 = await manager.GenerateSessionKeyAsync(accountAddress, validDays: 1);

            _output.WriteLine($"  Generated 3 session keys:");
            _output.WriteLine($"    Key 1 (30 days): {key1.Key}");
            _output.WriteLine($"    Key 2 (7 days): {key2.Key}");
            _output.WriteLine($"    Key 3 (1 day): {key3.Key}");

            // ============================================================
            // Test retrieval
            // ============================================================
            _output.WriteLine("\nPHASE 2: Key Retrieval");

            var retrieved = await manager.GetSessionKeyAsync(key1.Key);
            Assert.NotNull(retrieved);
            Assert.Equal(key1.Key, retrieved.Key);
            _output.WriteLine($"  Retrieved key 1: {retrieved.Key}");

            // ============================================================
            // Test GetSessionKeysForAccount
            // ============================================================
            _output.WriteLine("\nPHASE 3: Get All Keys for Account");

            var allKeys = await manager.GetSessionKeysForAccountAsync(accountAddress);
            Assert.Equal(3, allKeys.Length);
            _output.WriteLine($"  Total keys for account: {allKeys.Length}");

            // ============================================================
            // Test MarkRegistered
            // ============================================================
            _output.WriteLine("\nPHASE 4: Mark Keys as Registered");

            await manager.MarkRegisteredAsync(key1.Key);
            await manager.MarkRegisteredAsync(key2.Key);

            var active1 = await manager.GetSessionKeyAsync(key1.Key);
            var active2 = await manager.GetSessionKeyAsync(key2.Key);
            var inactive3 = await manager.GetSessionKeyAsync(key3.Key);

            Assert.True(active1!.IsActive);
            Assert.True(active2!.IsActive);
            Assert.False(inactive3!.IsActive);
            _output.WriteLine($"  Key 1 active: {active1.IsActive}");
            _output.WriteLine($"  Key 2 active: {active2.IsActive}");
            _output.WriteLine($"  Key 3 active: {inactive3.IsActive}");

            // ============================================================
            // Test GetBestSessionKey (should return key with longest validity among active)
            // ============================================================
            _output.WriteLine("\nPHASE 5: Get Best Session Key");

            var best = await manager.GetBestSessionKeyAsync(accountAddress);
            Assert.NotNull(best);
            Assert.Equal(key1.Key, best.Key);
            _output.WriteLine($"  Best key (longest validity): {best.Key}");
            _output.WriteLine($"  Valid until: {DateTimeOffset.FromUnixTimeSeconds((long)best.ValidUntil)}");

            // ============================================================
            // Test RemoveSessionKey
            // ============================================================
            _output.WriteLine("\nPHASE 6: Remove Session Key");

            await manager.RemoveSessionKeyAsync(key1.Key);
            var removed = await manager.GetSessionKeyAsync(key1.Key);
            Assert.Null(removed);
            _output.WriteLine($"  Key 1 removed: {removed == null}");

            var remainingKeys = await manager.GetSessionKeysForAccountAsync(accountAddress);
            Assert.Equal(2, remainingKeys.Length);
            _output.WriteLine($"  Remaining keys: {remainingKeys.Length}");

            _output.WriteLine("\n========== SESSION KEY STORE TEST COMPLETED ==========");
        }

        private byte[] CreateSalt(ulong value)
        {
            var saltBytes = new byte[32];
            var valueBytes = BitConverter.GetBytes(value);
            if (BitConverter.IsLittleEndian)
                Array.Reverse(valueBytes);
            Array.Copy(valueBytes, 0, saltBytes, 24, 8);
            return saltBytes;
        }

        private byte[] CreateInitData()
        {
            var validatorAddress = _ecdsaValidatorService.ContractAddress.HexToByteArray();
            var ownerAddress = _ownerAccount.Address.HexToByteArray();
            var initData = new byte[40];
            Array.Copy(validatorAddress, 0, initData, 0, 20);
            Array.Copy(ownerAddress, 0, initData, 20, 20);
            return initData;
        }
    }
}
