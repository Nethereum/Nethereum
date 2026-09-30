using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Signer;
using Nethereum.XUnitEthereumClients;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class BundleManagementE2ETests
    {
        private readonly BundlerTestFixture _fixture;

        public BundleManagementE2ETests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        public async Task E2E_MixedBundle_NewAndExistingAccounts_AllExecute()
        {
            var existingSalt = (ulong)Random.Shared.NextInt64();
            var (existingAccount, existingKey) = await _fixture.CreateFundedAccountAsync(existingSalt, 0.2m);

            var newSalt = (ulong)Random.Shared.NextInt64();
            var newOwnerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var newOwnerAddress = newOwnerKey.GetPublicAddress();
            var newAccountAddress = await _fixture.GetAccountAddressAsync(newOwnerAddress, newSalt);
            await _fixture.FundAccountAsync(newAccountAddress, 0.2m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(newOwnerAddress, newSalt);

            var executeFunction = new ExecuteFunction
            {
                Target = "0x" + new string('1', 40),
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var existingNonce = await _fixture.EntryPointService.GetNonceQueryAsync(existingAccount, BigInteger.Zero);
            var userOp1 = new UserOperation
            {
                Sender = existingAccount,
                CallData = executeFunction.GetCallData(),
                Nonce = existingNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var userOp2 = new UserOperation
            {
                Sender = newAccountAddress,
                InitCode = initCode,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 500_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, existingKey);
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, newOwnerKey);

            using var bundler = _fixture.CreateNewBundlerService();
            await bundler.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);
            await bundler.SendUserOperationAsync(packedOp2, _fixture.EntryPointService.ContractAddress);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Mixed bundle should succeed: {result.Error}");

            var newCode = await _fixture.Web3.Eth.GetCode.SendRequestAsync(newAccountAddress);
            Assert.NotEqual("0x", newCode);
        }

        [Fact]
        public async Task E2E_BundleWithMultipleOps_SameSender_NoncesCorrect()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            for (int i = 0; i < 3; i++)
            {
                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    Nonce = startNonce + i,
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                };

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            }

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle with sequential nonces should succeed: {result.Error}");

            var endNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce + 3, endNonce);
        }


        [Fact]
        public async Task E2E_SameSenderGapNonce_Rejected()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            var userOp0 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedOp0 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp0, accountKey);
            await bundler.SendUserOperationAsync(packedOp0, _fixture.EntryPointService.ContractAddress);

            var userOp2 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce + 2,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedOp2 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp2, accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
                await bundler.SendUserOperationAsync(packedOp2, _fixture.EntryPointService.ContractAddress));

            Assert.Contains("AA25", ex.Message);
        }

        [Fact]
        public async Task E2E_SameSenderReusedPendingNonce_NotFeeBump_Rejected()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            var userOp0 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedOp0 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp0, accountKey);
            await bundler.SendUserOperationAsync(packedOp0, _fixture.EntryPointService.ContractAddress);

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce + 1,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
            await bundler.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);

            var competingOp1 = new UserOperation
            {
                Sender = accountAddress,
                CallData = new ExecuteFunction
                {
                    Target = "0x" + new string('7', 40),
                    Value = 0,
                    Data = Array.Empty<byte>()
                }.GetCallData(),
                Nonce = startNonce + 1,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedCompeting = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(competingOp1, accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
                await bundler.SendUserOperationAsync(packedCompeting, _fixture.EntryPointService.ContractAddress));

            Assert.Equal(BundlerErrorCodes.InvalidFields, ex.Code);
            Assert.Contains("10%", ex.Message);
        }

        [Fact]
        public async Task E2E_SameSenderTooLowNonce_Rejected()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            var userOp0 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedOp0 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp0, accountKey);
            await bundler.SendUserOperationAsync(packedOp0, _fixture.EntryPointService.ContractAddress);

            var flushResult = await bundler.ExecuteBundleAsync();
            Assert.True(flushResult?.Success ?? false, $"Setup op should mine so the on-chain nonce advances: {flushResult?.Error}");

            var minedNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce + 1, minedNonce);

            var staleOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            var packedStale = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(staleOp, accountKey);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
                await bundler.SendUserOperationAsync(packedStale, _fixture.EntryPointService.ContractAddress));

            Assert.Contains("AA25", ex.Message);
        }

        [Fact]
        public async Task E2E_SameSenderChain_MiddleOpUnderpriced_BundlesOnlyUpToGap()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            var maxFeesPerGas = new BigInteger[] { 3_000_000_000, 1_000_000_000, 3_000_000_000 };
            var hashes = new string[3];

            for (int i = 0; i < 3; i++)
            {
                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = executeFunction.GetCallData(),
                    Nonce = startNonce + i,
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = maxFeesPerGas[i],
                    MaxPriorityFeePerGas = maxFeesPerGas[i]
                };
                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
                hashes[i] = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            }

            BigInteger minBaseFee = 2_000_000_000;
            var result = await bundler.ExecuteBundleAsync(minBaseFee);

            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle up to the gap should succeed: {result.Error}");

            var endNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce + 1, endNonce);

            var status0 = await bundler.GetUserOperationStatusAsync(hashes[0]);
            var status1 = await bundler.GetUserOperationStatusAsync(hashes[1]);
            var status2 = await bundler.GetUserOperationStatusAsync(hashes[2]);

            Assert.Equal(UserOpState.Included, status0.State);
            Assert.Equal(UserOpState.Pending, status1.State);
            Assert.Equal(UserOpState.Pending, status2.State);
        }

        [Fact]
        public async Task E2E_BundleWith5Operations_AllSucceed()
        {
            var accounts = new List<(string address, EthECKey key)>();

            for (int i = 0; i < 5; i++)
            {
                var salt = (ulong)Random.Shared.NextInt64();
                var account = await _fixture.CreateFundedAccountAsync(salt, 0.1m);
                accounts.Add(account);
            }

            var executeFunction = new ExecuteFunction
            {
                Target = "0x" + new string('2', 40),
                Value = 0,
                Data = Array.Empty<byte>()
            };

            using var bundler = _fixture.CreateNewBundlerService();

            foreach (var (address, key) in accounts)
            {
                var userOp = new UserOperation
                {
                    Sender = address,
                    CallData = executeFunction.GetCallData(),
                    CallGasLimit = 100_000,
                    VerificationGasLimit = 200_000,
                    PreVerificationGas = 50_000,
                    MaxFeePerGas = 2_000_000_000,
                    MaxPriorityFeePerGas = 1_000_000_000
                };

                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, key);
                await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            }

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"Bundle with 5 operations should succeed: {result.Error}");
        }

        [Fact]
        public async Task E2E_EmptyBundle_HandlesGracefully()
        {
            using var bundler = _fixture.CreateNewBundlerService();

            var result = await bundler.ExecuteBundleAsync();

            Assert.True(result == null || result.Success,
                "Empty bundle should handle gracefully");
        }

        [Fact]
        public async Task E2E_BundleFlush_ExecutesAllPending()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.3m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            using var bundler = _fixture.CreateNewBundlerService();

            var userOp1 = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                Nonce = startNonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp1 = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp1, accountKey);
            var hash1 = await bundler.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress);

            var statusBeforeFlush = await bundler.GetUserOperationStatusAsync(hash1);
            Assert.Equal(UserOpState.Pending, statusBeforeFlush.State);

            await bundler.FlushAsync();

            var statusAfterFlush = await bundler.GetUserOperationStatusAsync(hash1);
            Assert.True(statusAfterFlush.State == UserOpState.Included || statusAfterFlush.State == UserOpState.Failed,
                "After flush, operation should be included or failed");
        }

        [Fact]
        public async Task E2E_BundleStatusTracking_PendingToIncluded()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.2m);

            var executeFunction = new ExecuteFunction
            {
                Target = accountAddress,
                Value = 0,
                Data = Array.Empty<byte>()
            };

            var userOp = new UserOperation
            {
                Sender = accountAddress,
                CallData = executeFunction.GetCallData(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);

            using var bundler = _fixture.CreateNewBundlerService();

            var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash);

            var pendingStatus = await bundler.GetUserOperationStatusAsync(hash);
            Assert.Equal(UserOpState.Pending, pendingStatus.State);

            var result = await bundler.ExecuteBundleAsync();
            Assert.True(result?.Success ?? false);

            var includedStatus = await bundler.GetUserOperationStatusAsync(hash);
            Assert.Equal(UserOpState.Included, includedStatus.State);

            var receipt = await bundler.GetUserOperationReceiptAsync(hash);
            Assert.NotNull(receipt);
            Assert.True(receipt.Success);
        }


        private async Task<Nethereum.AccountAbstraction.Structs.PackedUserOperation> BuildRealSignedOpAsync(
            string sender, BigInteger nonce, EthECKey signerKey)
        {
            var executeFunction = new ExecuteFunction { Target = sender, Value = 0, Data = Array.Empty<byte>() };
            var userOp = new UserOperation
            {
                Sender = sender,
                CallData = executeFunction.GetCallData(),
                Nonce = nonce,
                CallGasLimit = 100_000,
                VerificationGasLimit = 200_000,
                PreVerificationGas = 50_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000
            };
            return await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, signerKey);
        }

        [Fact]
        public async Task E2E_SameSenderChain_FrontOpExpired_BundlesNothingForThatSender()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);
            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var mempool = new InMemoryUserOpMempool();
            var hashes = new[] { "0x" + new string('a', 64), "0x" + new string('b', 64), "0x" + new string('c', 64) };

            for (var i = 0; i < 3; i++)
            {
                var packedOp = await BuildRealSignedOpAsync(accountAddress, startNonce + i, accountKey);
                await mempool.AddAsync(new MempoolEntry
                {
                    UserOpHash = hashes[i],
                    UserOperation = packedOp,
                    EntryPoint = _fixture.EntryPointService.ContractAddress,
                    Priority = 1_000_000_000,
                    ValidUntil = i == 0 ? 1UL : null
                });
            }

            using var bundler = new BundlerService(
                _fixture.Web3, _fixture.BundlerConfig, mempool, validator: null, executor: null);

            var result = await bundler.ExecuteBundleAsync();

            Assert.Null(result);

            var onChainNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce, onChainNonce);

            var allPending = await mempool.GetAllPendingAsync();
            Assert.Contains(allPending, e => e.UserOpHash == hashes[1]);
            Assert.Contains(allPending, e => e.UserOpHash == hashes[2]);
            Assert.Equal(MempoolEntryState.Pending, (await mempool.GetAsync(hashes[1]))!.State);
            Assert.Equal(MempoolEntryState.Pending, (await mempool.GetAsync(hashes[2]))!.State);
        }

        [Fact]
        public async Task E2E_SameSenderChain_FrontOpDropped_BundlesNothingForThatSender()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);
            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var mempool = new InMemoryUserOpMempool();
            var hashes = new[] { "0x" + new string('d', 64), "0x" + new string('e', 64), "0x" + new string('f', 64) };

            for (var i = 0; i < 3; i++)
            {
                var packedOp = await BuildRealSignedOpAsync(accountAddress, startNonce + i, accountKey);
                await mempool.AddAsync(new MempoolEntry
                {
                    UserOpHash = hashes[i],
                    UserOperation = packedOp,
                    EntryPoint = _fixture.EntryPointService.ContractAddress,
                    Priority = 1_000_000_000
                });
            }

            using var bundler = new BundlerService(
                _fixture.Web3, _fixture.BundlerConfig, mempool, validator: null, executor: null);

            Assert.True(await bundler.DropUserOperationAsync(hashes[0]));

            var result = await bundler.ExecuteBundleAsync();

            Assert.Null(result);

            var onChainNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce, onChainNonce);

            var allPending = await mempool.GetAllPendingAsync();
            Assert.Contains(allPending, e => e.UserOpHash == hashes[1]);
            Assert.Contains(allPending, e => e.UserOpHash == hashes[2]);
            Assert.Equal(MempoolEntryState.Pending, (await mempool.GetAsync(hashes[1]))!.State);
            Assert.Equal(MempoolEntryState.Pending, (await mempool.GetAsync(hashes[2]))!.State);
        }

        [Fact]
        public async Task E2E_SameSenderChain_StaleFrontAlreadyMinedElsewhere_AnchorsFromOnChainNonce()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);
            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var op0 = await BuildRealSignedOpAsync(accountAddress, startNonce, accountKey);
            var op1 = await BuildRealSignedOpAsync(accountAddress, startNonce + 1, accountKey);
            var op2 = await BuildRealSignedOpAsync(accountAddress, startNonce + 2, accountKey);

            var mempool = new InMemoryUserOpMempool();
            await mempool.AddAsync(new MempoolEntry { UserOpHash = "0x" + new string('1', 64), UserOperation = op0, EntryPoint = _fixture.EntryPointService.ContractAddress, Priority = 1_000_000_000 });
            await mempool.AddAsync(new MempoolEntry { UserOpHash = "0x" + new string('2', 64), UserOperation = op1, EntryPoint = _fixture.EntryPointService.ContractAddress, Priority = 1_000_000_000 });
            await mempool.AddAsync(new MempoolEntry { UserOpHash = "0x" + new string('3', 64), UserOperation = op2, EntryPoint = _fixture.EntryPointService.ContractAddress, Priority = 1_000_000_000 });

            using (var outsideBundler = _fixture.CreateNewBundlerService())
            {
                await outsideBundler.SendUserOperationAsync(op0, _fixture.EntryPointService.ContractAddress);
                var outsideResult = await outsideBundler.ExecuteBundleAsync();
                Assert.True(outsideResult?.Success ?? false, $"setup op0 should mine: {outsideResult?.Error}");
            }

            var advancedNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce + 1, advancedNonce);

            using var bundler = new BundlerService(
                _fixture.Web3, _fixture.BundlerConfig, mempool, validator: null, executor: null);

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.True(result.Success, $"op1/op2 should bundle from the real on-chain nonce: {result.Error}");

            var finalNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);
            Assert.Equal(startNonce + 3, finalNonce);
        }


        [Fact]
        public async Task E2E_SameSenderChain_FrontFailsAttributableFault_SuccessorsStayPendingUnpenalized()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 0.5m);
            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var wrongKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var reputationService = new InMemoryReputationService();

            using var bundler = new BundlerService(
                _fixture.Web3, _fixture.BundlerConfig, mempool: null, validator: null, executor: null, reputationService);

            var hashes = new string[3];
            for (var i = 0; i < 3; i++)
            {
                var signer = i == 0 ? wrongKey : accountKey;
                var packedOp = await BuildRealSignedOpAsync(accountAddress, startNonce + i, signer);
                hashes[i] = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
            }

            var result = await bundler.ExecuteBundleAsync();

            Assert.NotNull(result);
            Assert.False(result.Success);
            Assert.Equal(0, result.FailedOpIndex);
            Assert.Contains("AA24", result.FailedOpReason ?? result.Error ?? "");

            Assert.Equal(UserOpState.Failed, (await bundler.GetUserOperationStatusAsync(hashes[0])).State);
            Assert.Equal(UserOpState.Pending, (await bundler.GetUserOperationStatusAsync(hashes[1])).State);
            Assert.Equal(UserOpState.Pending, (await bundler.GetUserOperationStatusAsync(hashes[2])).State);

            var reputation = await bundler.GetReputationAsync(accountAddress);
            Assert.True(reputation.OpsFailed >= 1, "the evicted front op's sender should be penalized");
        }


        [Fact]
        public async Task E2E_SameSenderChain_CumulativePrefundExceedsFunds_SecondOpRejectedAtAdmission()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1.0m);
            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var maxFeePerGas = new BigInteger(1_000_000_000);
            const int callGasLimit = 100_000;
            const int verificationGasLimit = 100_000;
            var perOpPrefund = Nethereum.Util.UnitConversion.Convert.ToWei(0.6m);
            var totalGasForPerOpPrefund = perOpPrefund / maxFeePerGas;
            var preVerificationGas = totalGasForPerOpPrefund - callGasLimit - verificationGasLimit;

            Func<BigInteger, Task<Nethereum.AccountAbstraction.Structs.PackedUserOperation>> buildOp = async nonce =>
            {
                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = new ExecuteFunction { Target = accountAddress, Value = 0, Data = Array.Empty<byte>() }.GetCallData(),
                    Nonce = nonce,
                    CallGasLimit = callGasLimit,
                    VerificationGasLimit = verificationGasLimit,
                    PreVerificationGas = preVerificationGas,
                    MaxFeePerGas = maxFeePerGas,
                    MaxPriorityFeePerGas = maxFeePerGas
                };
                return await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
            };

            using var bundler = new BundlerService(_fixture.Web3, _fixture.BundlerConfig, mempool: null, validator: null, executor: null);

            var packedOp0 = await buildOp(startNonce);
            var hash0 = await bundler.SendUserOperationAsync(packedOp0, _fixture.EntryPointService.ContractAddress);
            Assert.NotNull(hash0);

            var packedOp1 = await buildOp(startNonce + 1);

            var ex = await Assert.ThrowsAsync<BundlerRpcException>(async () =>
                await bundler.SendUserOperationAsync(packedOp1, _fixture.EntryPointService.ContractAddress));

            Assert.Contains("AA21", ex.Message);

            var pendingOps = await bundler.GetPendingUserOperationsAsync();
            Assert.Single(pendingOps);
            Assert.Equal(hash0, pendingOps[0].UserOpHash);

            var reputation = await bundler.GetReputationAsync(accountAddress);
            Assert.Equal(0, reputation.OpsFailed);
        }

        [Fact]
        public async Task E2E_SameSenderChain_FundedForFullChain_AllAdmitted()
        {
            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt, 1.0m);
            var startNonce = await _fixture.EntryPointService.GetNonceQueryAsync(accountAddress, BigInteger.Zero);

            var maxFeePerGas = new BigInteger(1_000_000_000);
            const int callGasLimit = 100_000;
            const int verificationGasLimit = 100_000;
            var perOpPrefund = Nethereum.Util.UnitConversion.Convert.ToWei(0.2m);
            var totalGasForPerOpPrefund = perOpPrefund / maxFeePerGas;
            var preVerificationGas = totalGasForPerOpPrefund - callGasLimit - verificationGasLimit;

            using var bundler = _fixture.CreateNewBundlerService();

            for (var i = 0; i < 3; i++)
            {
                var userOp = new UserOperation
                {
                    Sender = accountAddress,
                    CallData = new ExecuteFunction { Target = accountAddress, Value = 0, Data = Array.Empty<byte>() }.GetCallData(),
                    Nonce = startNonce + i,
                    CallGasLimit = callGasLimit,
                    VerificationGasLimit = verificationGasLimit,
                    PreVerificationGas = preVerificationGas,
                    MaxFeePerGas = maxFeePerGas,
                    MaxPriorityFeePerGas = maxFeePerGas
                };
                var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
                var hash = await bundler.SendUserOperationAsync(packedOp, _fixture.EntryPointService.ContractAddress);
                Assert.NotNull(hash);
            }

            var pendingOps = await bundler.GetPendingUserOperationsAsync();
            Assert.Equal(3, pendingOps.Length);
        }
    }
}
