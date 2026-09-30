using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Documentation;
using Nethereum.Web3.Accounts;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Bundler
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class BundlerServiceTests
    {
        private readonly BundlerTestFixture _fixture;

        public BundlerServiceTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Send UserOperation returns UserOp hash", Order = 1)]
        public async Task SendUserOperation_WithValidOperation_ReturnsUserOpHash()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            var userOpHash = await _fixture.BundlerService.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(userOpHash);
            Assert.StartsWith("0x", userOpHash);
            Assert.Equal(66, userOpHash.Length);
        }

        [Fact]
        public async Task SendUserOperation_WithBlacklistedSender_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BeneficiaryAddress,
                AutoBundleIntervalMs = 0,
                StrictValidation = false,
                SimulateValidation = false,
                BlacklistedAddresses = new HashSet<string> { accountAddress.ToLowerInvariant() }
            };

            using var bundler = new BundlerService(_fixture.Web3, config);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.Reputation, ex.Code);
            Assert.Contains("blacklisted", ex.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task SendUserOperation_WithUnsupportedEntryPoint_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            var fakeEntryPoint = "0x0000000000000000000000000000000000000001";

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                _fixture.BundlerService.SendUserOperationAsync(userOp, fakeEntryPoint));

            Assert.Equal(BundlerErrorCodes.InvalidFields, ex.Code);
            Assert.Contains("Unsupported EntryPoint", ex.Message);
        }

        [Fact]
        public async Task SendUserOperation_Duplicate_ThrowsException()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var hash1 = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            Assert.NotNull(hash1);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.InvalidFields, ex.Code);
            Assert.Contains("duplicate", ex.Message.ToLowerInvariant());
        }

        [Fact]
        public async Task SendUserOperation_UnstakedSenderAtMempoolLimit_RejectsAdditionalOp()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            using var bundler = _fixture.CreateNewBundlerService();
            var maxCount = _fixture.BundlerConfig.MaxUnstakedSenderMempoolCount;

            for (var i = 0; i < maxCount; i++)
            {
                var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                    new UserOperation
                    {
                        Sender = accountAddress,
                        Nonce = (BigInteger)(i + 1) << 64,
                        CallData = Array.Empty<byte>(),
                        CallGasLimit = 100_000,
                        VerificationGasLimit = 100_000,
                        PreVerificationGas = 100_000
                    },
                    accountKey);

                await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            }

            var pendingBefore = await bundler.GetPendingUserOperationsAsync();
            Assert.Equal(maxCount, pendingBefore.Length);

            var overLimitOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    Nonce = (BigInteger)(maxCount + 1) << 64,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                bundler.SendUserOperationAsync(overLimitOp, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.InsufficientStake, ex.Code);

            var pendingAfter = await bundler.GetPendingUserOperationsAsync();
            Assert.Equal(maxCount, pendingAfter.Length);
        }

        [Fact]
        public async Task SendUserOperation_StakedSenderBeyondUnstakedMempoolLimit_AllAccepted()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var stakeAmountEther = Nethereum.Web3.Web3.Convert.FromWei(_fixture.BundlerConfig.MinStake);
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, stakeAmountEther + 0.5m);

            var ownerAddress = accountKey.GetPublicAddress();
            await _fixture.FundAccountAsync(ownerAddress, 0.1m);

            var ownerWeb3 = new Nethereum.Web3.Web3(
                new Account(accountKey.GetPrivateKey(), _fixture.ChainId),
                _fixture.Web3.Client);
            var simpleAccountService = new SimpleAccountService(ownerWeb3, accountAddress);

            var addStakeCallData = new AddStakeFunction
            {
                UnstakeDelaySec = _fixture.BundlerConfig.MinUnstakeDelaySec
            }.GetCallData();

            await simpleAccountService.ExecuteRequestAndWaitForReceiptAsync(
                _fixture.EntryPointService.ContractAddress,
                _fixture.BundlerConfig.MinStake,
                addStakeCallData);

            using var bundler = _fixture.CreateNewBundlerService();
            var beyondUnstakedLimit = _fixture.BundlerConfig.MaxUnstakedSenderMempoolCount + 1;

            for (var i = 0; i < beyondUnstakedLimit; i++)
            {
                var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                    new UserOperation
                    {
                        Sender = accountAddress,
                        Nonce = (BigInteger)(i + 1) << 64,
                        CallData = Array.Empty<byte>(),
                        CallGasLimit = 100_000,
                        VerificationGasLimit = 100_000,
                        PreVerificationGas = 100_000
                    },
                    accountKey);

                await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            }

            var pending = await bundler.GetPendingUserOperationsAsync();
            Assert.Equal(beyondUnstakedLimit, pending.Length);
        }

        [Fact]
        public async Task SendUserOperation_PaymasterDepositCoversOnlyTwoOps_RejectsThirdWithCumulativeDepositTooLow()
        {
            var paymasterDeployment = new Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition.TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Nethereum.Web3.Web3)_fixture.Web3, paymasterDeployment);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            using var bundler = _fixture.CreateNewBundlerService();

            var packedOps = new List<Nethereum.AccountAbstraction.Structs.PackedUserOperation>();
            for (var i = 0; i < 3; i++)
            {
                var salt = (ulong)Random.Shared.NextInt64();
                var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                    new UserOperation
                    {
                        Sender = accountAddress,
                        CallData = Array.Empty<byte>(),
                        CallGasLimit = 100_000,
                        VerificationGasLimit = 100_000,
                        PreVerificationGas = 100_000,
                        MaxFeePerGas = 2_000_000_000,
                        MaxPriorityFeePerGas = 1_000_000_000,
                        Paymaster = paymasterAddress,
                        PaymasterVerificationGasLimit = 50_000,
                        PaymasterPostOpGasLimit = 50_000,
                        PaymasterData = Array.Empty<byte>()
                    },
                    accountKey);

                packedOps.Add(packedOp);
            }

            var perOpCost = (BigInteger)(100_000 + 100_000 + 100_000 + 50_000 + 50_000) * 2_000_000_000;
            var depositAmount = perOpCost * packedOps.Count - 1;

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterAddress,
                    AmountToSend = depositAmount
                });

            var hash1 = await bundler.SendUserOperationAsync(packedOps[0], _fixture.EntryPointService.ContractAddress);
            var hash2 = await bundler.SendUserOperationAsync(packedOps[1], _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash1);
            Assert.NotNull(hash2);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(() =>
                bundler.SendUserOperationAsync(packedOps[2], _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.PaymasterDepositTooLow, ex.Code);
            Assert.Contains("too low", ex.Message, StringComparison.OrdinalIgnoreCase);

            var pending = await bundler.GetPendingUserOperationsAsync();
            Assert.Equal(2, pending.Length);
        }

        [Fact]
        public async Task SendUserOperation_PaymasterDepositCoversAllOps_AllAccepted()
        {
            var paymasterDeployment = new Nethereum.AccountAbstraction.IntegrationTests.TestPaymasterAcceptAll.ContractDefinition.TestPaymasterAcceptAllDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress
            };
            var paymasterService = await TestPaymasterAcceptAllService.DeployContractAndGetServiceAsync(
                (Nethereum.Web3.Web3)_fixture.Web3, paymasterDeployment);
            var paymasterAddress = paymasterService.ContractHandler.ContractAddress;

            using var bundler = _fixture.CreateNewBundlerService();

            var packedOps = new List<Nethereum.AccountAbstraction.Structs.PackedUserOperation>();
            for (var i = 0; i < 3; i++)
            {
                var salt = (ulong)Random.Shared.NextInt64();
                var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                    new UserOperation
                    {
                        Sender = accountAddress,
                        CallData = Array.Empty<byte>(),
                        CallGasLimit = 100_000,
                        VerificationGasLimit = 100_000,
                        PreVerificationGas = 100_000,
                        MaxFeePerGas = 2_000_000_000,
                        MaxPriorityFeePerGas = 1_000_000_000,
                        Paymaster = paymasterAddress,
                        PaymasterVerificationGasLimit = 50_000,
                        PaymasterPostOpGasLimit = 50_000,
                        PaymasterData = Array.Empty<byte>()
                    },
                    accountKey);

                packedOps.Add(packedOp);
            }

            var perOpCost = (BigInteger)(100_000 + 100_000 + 100_000 + 50_000 + 50_000) * 2_000_000_000;
            var depositAmount = perOpCost * packedOps.Count;

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = paymasterAddress,
                    AmountToSend = depositAmount
                });

            foreach (var packedOp in packedOps)
            {
                var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                Assert.NotNull(hash);
            }

            var pending = await bundler.GetPendingUserOperationsAsync();
            Assert.Equal(3, pending.Length);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Get UserOperation by hash after submission", Order = 2)]
        public async Task GetUserOperationByHash_AfterSubmission_ReturnsOperation()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var retrievedOp = await bundler.GetUserOperationByHashAsync(userOpHash);

            Assert.NotNull(retrievedOp);
            Assert.Equal(userOpHash, retrievedOp.UserOpHash);
            Assert.Equal(accountAddress.ToLowerInvariant(), retrievedOp.UserOperation.Sender?.ToLowerInvariant());
        }

        [Fact]
        public async Task GetUserOperationByHash_WithUnknownHash_ReturnsNull()
        {
            var unknownHash = "0x" + new string('0', 64);

            var result = await _fixture.BundlerService.GetUserOperationByHashAsync(unknownHash);

            Assert.Null(result);
        }

        [Fact]
        public async Task GetUserOperationStatus_AfterSubmission_ReturnsPendingState()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var status = await bundler.GetUserOperationStatusAsync(userOpHash);

            Assert.NotNull(status);
            Assert.Equal(userOpHash, status.UserOpHash);
            Assert.Equal(UserOpState.Pending, status.State);
        }

        [Fact]
        public async Task GetPendingUserOperations_AfterSubmissions_ReturnsAllPending()
        {
            using var bundler = _fixture.CreateNewBundlerService();

            var pendingBefore = await bundler.GetPendingUserOperationsAsync();
            var initialCount = pendingBefore.Length;

            var salt1 = (ulong)Random.Shared.NextInt64();
            var (account1, key1) = await _fixture.CreateFundedAccountAsync(salt1);

            var salt2 = (ulong)Random.Shared.NextInt64();
            var key2 = new EthECKey(TestAccounts.Account3PrivateKey);
            var owner2 = key2.GetPublicAddress();
            await _fixture.FundAccountAsync(owner2, 0.1m);
            var result2 = await _fixture.AccountFactoryService.CreateAndDeployAccountAsync(
                owner2, owner2, _fixture.EntryPointService.ContractAddress, key2, 0.01m, salt2);

            var execute1 = new ExecuteFunction { Target = account1, Value = 0, Data = Array.Empty<byte>() };
            var userOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation { Sender = account1, CallData = execute1.GetCallData(), CallGasLimit = 100_000, VerificationGasLimit = 100_000, PreVerificationGas = 100_000 },
                key1);

            var execute2 = new ExecuteFunction { Target = result2.AccountAddress, Value = 0, Data = Array.Empty<byte>() };
            var userOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation { Sender = result2.AccountAddress, CallData = execute2.GetCallData(), CallGasLimit = 100_000, VerificationGasLimit = 100_000, PreVerificationGas = 100_000 },
                key2);

            await bundler.SendUserOperationAsync(userOp1, _fixture.EntryPointService.ContractAddress);
            await bundler.SendUserOperationAsync(userOp2, _fixture.EntryPointService.ContractAddress);

            var pending = await bundler.GetPendingUserOperationsAsync();

            Assert.Equal(initialCount + 2, pending.Length);
        }

        [Fact]
        public async Task DropUserOperation_WithValidHash_RemovesFromMempool()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 100_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var dropped = await bundler.DropUserOperationAsync(userOpHash);
            Assert.True(dropped);

            var status = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Dropped, status.State);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Query supported entry points", Order = 3)]
        public async Task SupportedEntryPoints_ReturnsConfiguredEntryPoints()
        {
            var entryPoints = await _fixture.BundlerService.SupportedEntryPointsAsync();

            Assert.NotNull(entryPoints);
            Assert.Single(entryPoints);
            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLowerInvariant(),
                entryPoints[0].ToLowerInvariant());
        }

        [Fact]
        public async Task ChainId_ReturnsConfiguredChainId()
        {
            var chainId = await _fixture.BundlerService.ChainIdAsync();

            Assert.Equal(_fixture.ChainId, chainId);
        }

        [Fact]
        public async Task GetStats_ReturnsValidStatistics()
        {
            var stats = await _fixture.BundlerService.GetStatsAsync();

            Assert.NotNull(stats);
            Assert.True(stats.StartedAt <= DateTimeOffset.UtcNow);
        }

        [Fact]
        [NethereumDocExample(DocSection.AccountAbstraction, "run-bundler", "Flush executes pending operations", Order = 4)]
        public async Task Flush_ExecutesPendingOperations()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.1m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 200_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 100_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOpHash = await bundler.SendUserOperationAsync(
                userOp,
                _fixture.EntryPointService.ContractAddress);

            var statusBefore = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Pending, statusBefore.State);

            var txHash = await bundler.FlushAsync();

            Assert.NotNull(txHash);
            Assert.StartsWith("0x", txHash);

            var statusAfter = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.True(statusAfter.State == UserOpState.Included || statusAfter.State == UserOpState.Failed);
        }

        [Fact]
        public async Task ExecuteBundle_GriefingOp_IsEvictedAloneAndSurvivorsAreIncluded()
        {
            var saltA = (ulong)Random.Shared.NextInt64();
            var saltB = (ulong)Random.Shared.NextInt64();
            var (accountA, keyA) = await _fixture.CreateFundedAccountAsync(saltA);
            var (accountB, _) = await _fixture.CreateFundedAccountAsync(saltB);

            var validOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountA,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                keyA);

            var griefingOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountB,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                EthECKey.GenerateKey());

            using var bundler = new BundlerService(
                _fixture.Web3, _fixture.BundlerConfig, null, null, null,
                new Nethereum.AccountAbstraction.Bundler.Reputation.InMemoryReputationService());

            var validHash = await bundler.SendUserOperationAsync(
                validOp, _fixture.EntryPointService.ContractAddress);
            var griefingHash = await bundler.SendUserOperationAsync(
                griefingOp, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result!.Success, $"surviving bundle should mine, got: {result.Error}");

            var validStatus = await bundler.GetUserOperationStatusAsync(validHash);
            Assert.Equal(UserOpState.Included, validStatus.State);

            var griefingStatus = await bundler.GetUserOperationStatusAsync(griefingHash);
            Assert.Equal(UserOpState.Failed, griefingStatus.State);
            Assert.Contains("AA24", griefingStatus.Error);

            var griefingReputation = await bundler.GetReputationAsync(accountB.ToLowerInvariant());
            Assert.True(griefingReputation.OpsFailed >= 1, "the griefing sender must be penalized");

            var survivorReputation = await bundler.GetReputationAsync(accountA.ToLowerInvariant());
            Assert.Equal(0, survivorReputation.OpsFailed);
            Assert.True(survivorReputation.OpsIncluded >= 1, "the surviving sender must not be penalized");
        }

        [Fact]
        public async Task GetUserOperationReceipt_AfterClearState_AnswersStatelesslyFromLogs()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var userOpHash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();
            Assert.True(result?.Success, result?.Error);

            await bundler.ClearStateAsync();

            var receipt = await bundler.GetUserOperationReceiptAsync(userOpHash);

            Assert.NotNull(receipt);
            Assert.True(receipt!.Success);
            Assert.Equal(accountAddress.ToLowerInvariant(), receipt.Sender.ToLowerInvariant());
            Assert.True(receipt.ActualGasUsed.Value > 0, "gas actuals must come from the UserOperationEvent");
            Assert.True(receipt.ActualGasCost.Value > 0);
            Assert.NotNull(receipt.Logs);
            Assert.True(receipt.Logs.Count > 0, "the receipt must carry the operation's log slice");
        }

        [Fact]
        public async Task GetUserOperationReceipt_ExecutionReverted_ReportsFailureFromEvent()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1m);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x" + new string('4', 40),
                Value = Nethereum.Web3.Web3.Convert.ToWei(1_000_000),
                Data = Array.Empty<byte>()
            };

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 150_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                accountKey);

            using var bundler = _fixture.CreateNewBundlerService();
            var userOpHash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();
            Assert.True(result?.Success, $"the bundle mines even when the op's execution fails: {result?.Error}");

            var receipt = await bundler.GetUserOperationReceiptAsync(userOpHash);

            Assert.NotNull(receipt);
            Assert.False(receipt!.Success, "a receipt must never report success for a reverted operation");
            Assert.True(receipt.ActualGasUsed.Value > 0);
        }

        [Fact]
        public async Task GetUserOperationReceipt_FailedEntryWithOnChainEvent_HealsToIncluded()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var userOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(
                new UserOperation
                {
                    Sender = accountAddress,
                    CallData = Array.Empty<byte>(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                },
                accountKey);

            var mempool = new InMemoryUserOpMempool();
            using var bundler = new BundlerService(_fixture.Web3, _fixture.BundlerConfig, mempool, null, null);

            var userOpHash = await bundler.SendUserOperationAsync(userOp, _fixture.EntryPointService.ContractAddress);
            var result = await bundler.ExecuteBundleAsync();
            Assert.True(result?.Success, result?.Error);

            await mempool.MarkFailedAsync(new[] { userOpHash }, "AA25: invalid nonce (receipt timeout race)");

            var receipt = await bundler.GetUserOperationReceiptAsync(userOpHash);

            Assert.NotNull(receipt);
            Assert.True(receipt!.Success, "the on-chain UserOperationEvent is authoritative over the stale Failed state");

            var status = await bundler.GetUserOperationStatusAsync(userOpHash);
            Assert.Equal(UserOpState.Included, status.State);
        }
    }
}
