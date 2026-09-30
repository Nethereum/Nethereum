using System.Collections.Concurrent;
using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Reputation;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.Contracts;
using Nethereum.EVM.Execution;
using Nethereum.Geth.RPC.GethEth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler
{
    public class BundlerService : IBundlerServiceExtended, IDisposable
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly IUserOpMempool _mempool;
        private readonly IUserOpValidator _validator;
        private readonly IBundleExecutor _executor;
        private readonly UserOperationReceiptService _receiptService;
        private readonly IReputationService? _reputationService;
        private readonly IStakingInfoService _stakingInfo;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();

        private readonly ConcurrentDictionary<string, UserOperationReceipt> _receipts = new();
        private readonly ConcurrentDictionary<string, ReputationEntry> _inMemoryReputation = new();
        private readonly BundlerStats _stats = new() { StartedAt = DateTimeOffset.UtcNow };

        private readonly SemaphoreSlim _admissionGate = new(1, 1);

        private readonly SemaphoreSlim _bundleGate = new(1, 1);

        private readonly SemaphoreSlim _reputationDecayGate = new(1, 1);

        private readonly ILogger? _logger;

        private Timer? _autoBundleTimer;
        private Timer? _reputationDecayTimer;
        private volatile BundlingMode _bundlingMode = BundlingMode.Auto;
        private BigInteger? _chainId;
        private bool _disposed;

        public BundlerService(IWeb3 web3, BundlerConfig config)
            : this(web3, config, null, null, null, null)
        {
        }

        public BundlerService(
            IWeb3 web3,
            BundlerConfig config,
            IUserOpMempool? mempool,
            IUserOpValidator? validator,
            IBundleExecutor? executor)
            : this(web3, config, mempool, validator, executor, null)
        {
        }

        public BundlerService(
            IWeb3 web3,
            BundlerConfig config,
            IUserOpMempool? mempool,
            IUserOpValidator? validator,
            IBundleExecutor? executor,
            IReputationService? reputationService,
            ILogger? logger = null)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _logger = logger;
            _mempool = mempool ?? new InMemoryUserOpMempool(config.MaxMempoolSize);
            _validator = validator ?? new UserOpValidator(web3, config, null, null, _mempool);
            _executor = executor ?? new BundleExecutor(web3, config);
            _receiptService = new UserOperationReceiptService(web3);
            _reputationService = reputationService;
            _stakingInfo = new StakingInfoService(web3, config);

            foreach (var ep in config.SupportedEntryPoints)
            {
                _entryPoints[ep.ToLowerInvariant()] = new EntryPointService(web3, ep);
            }

            if (config.AutoBundleIntervalMs > 0)
            {
                _autoBundleTimer = new Timer(
                    AutoBundleCallback,
                    null,
                    config.AutoBundleIntervalMs,
                    config.AutoBundleIntervalMs);
            }

            if (_reputationService != null && config.ReputationDecayIntervalMs > 0)
            {
                _reputationDecayTimer = new Timer(
                    ReputationDecayCallback,
                    null,
                    config.ReputationDecayIntervalMs,
                    config.ReputationDecayIntervalMs);
            }
        }

        public async Task<string> SendUserOperationAsync(PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null)
        {
            ValidateEntryPoint(entryPoint);

            if (_config.BlacklistedAddresses.Contains(userOp.Sender?.ToLowerInvariant() ?? ""))
            {
                throw new BundlerRpcException(
                    BundlerErrorCodes.Reputation,
                    $"Sender {userOp.Sender} is blacklisted",
                    new { sender = userOp.Sender });
            }

            if (_reputationService != null && !_config.UnsafeMode)
            {
                await CheckReputationAsync(userOp);
            }

            var accessedStorageAddresses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var validationResult = await _validator.ValidateAsync(userOp, entryPoint, accessedStorageAddresses, eip7702Auth);
            if (!validationResult.IsValid)
            {
                throw BundlerRpcException.FromValidationResult(validationResult);
            }

            var userOpHash = await CalculateUserOpHashAsync(userOp, entryPoint, eip7702Auth);

            var (maxPriorityFee, _) = userOp.UnpackGasFees();

            var entry = new MempoolEntry
            {
                UserOpHash = userOpHash,
                UserOperation = userOp,
                EntryPoint = entryPoint,
                Priority = maxPriorityFee,
                Prefund = CalculatePrefund(userOp),
                Factory = ExtractFactory(userOp.InitCode),
                Paymaster = ExtractPaymaster(userOp.PaymasterAndData),
                ValidUntil = validationResult.ValidUntil > 0 ? validationResult.ValidUntil : null,
                ValidAfter = validationResult.ValidAfter > 0 ? validationResult.ValidAfter : null,
                AccessedStorageAddresses = accessedStorageAddresses,
                Eip7702Auth = eip7702Auth
            };

            MempoolEntry? replacedEntry;
            MempoolAddOutcome addOutcome;

            await _admissionGate.WaitAsync();
            try
            {
                replacedEntry = await FindPendingBySenderAndNonceAsync(userOp.Sender, userOp.Nonce, entryPoint);

                await CheckPaymasterDepositAsync(entry, entryPoint, replacedEntry);
                await CheckSenderPrefundAsync(entry, entryPoint, replacedEntry);

                if (replacedEntry == null)
                {
                    await CheckSenderMempoolLimitAsync(userOp.Sender, entryPoint);
                    await CheckMultipleRolesViolationAsync(entry);
                }

                addOutcome = await _mempool.AddAsync(entry);
            }
            finally
            {
                _admissionGate.Release();
            }

            switch (addOutcome)
            {
                case MempoolAddOutcome.RejectedDuplicate:
                    throw new BundlerRpcException(
                        BundlerErrorCodes.InvalidFields,
                        "Duplicate UserOperation: already known or its (sender, nonce) is pending inclusion");
                case MempoolAddOutcome.RejectedUnderpriced:
                    throw new BundlerRpcException(
                        BundlerErrorCodes.InvalidFields,
                        "Replacement UserOperation must have at least 10% higher maxFeePerGas and maxPriorityFeePerGas");
                case MempoolAddOutcome.RejectedFull:
                    throw new BundlerRpcException(
                        BundlerErrorCodes.InvalidFields,
                        "Mempool is full");
            }

            if (_reputationService != null)
            {
                if (addOutcome == MempoolAddOutcome.Replaced && replacedEntry != null)
                {
                    await RevertSeenReputationAsync(replacedEntry, entryPoint);
                }

                await RecordSeenReputationAsync(entry, entryPoint);
            }

            return userOpHash;
        }

        private async Task<MempoolEntry?> FindPendingBySenderAndNonceAsync(string? sender, BigInteger nonce, string entryPoint)
        {
            if (string.IsNullOrEmpty(sender)) return null;

            var pending = await _mempool.GetBySenderAsync(sender);
            return pending.FirstOrDefault(e =>
                e.State == MempoolEntryState.Pending &&
                e.UserOperation.Nonce == nonce &&
                e.EntryPoint.Equals(entryPoint, StringComparison.OrdinalIgnoreCase));
        }

        public async Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(UserOperation userOp, string entryPoint)
        {
            ValidateEntryPoint(entryPoint);

            var gasEstimator = new SimulationGasEstimator(_web3, _config, _logger);
            return await gasEstimator.EstimateAsync(userOp, entryPoint);
        }

        public async Task<UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash)
        {
            if (_receipts.TryGetValue(userOpHash, out var cached))
            {
                return cached;
            }

            var entry = await _mempool.GetAsync(userOpHash);

            UserOperationReceipt? receipt = null;

            if (entry?.State == MempoolEntryState.Included && entry.TransactionHash != null)
            {
                var txReceipt = await _web3.Eth.Transactions.GetTransactionReceipt
                    .SendRequestAsync(entry.TransactionHash);
                if (txReceipt != null)
                {
                    receipt = _receiptService.BuildFromTransactionReceipt(
                        txReceipt, userOpHash, entry.EntryPoint);
                }
            }

            if (receipt == null)
            {
                receipt = await FindReceiptByLogScanAsync(userOpHash, entry?.EntryPoint);

                if (receipt != null && entry != null && entry.State != MempoolEntryState.Included)
                {
                    await _mempool.MarkIncludedAsync(
                        new[] { userOpHash },
                        receipt.Receipt.TransactionHash,
                        receipt.Receipt.BlockNumber?.Value ?? 0,
                        receipt.Receipt.BlockHash);
                }
            }

            if (receipt != null)
            {
                _receipts[userOpHash] = receipt;
            }

            return receipt;
        }

        private async Task<UserOperationReceipt?> FindReceiptByLogScanAsync(string userOpHash, string? knownEntryPoint)
        {
            var entryPoints = knownEntryPoint != null
                ? new[] { knownEntryPoint }
                : _config.SupportedEntryPoints;

            foreach (var entryPoint in entryPoints)
            {
                var receipt = await _receiptService.FindByLogScanAsync(
                    userOpHash, entryPoint, _config.ReceiptLogLookbackBlocks);
                if (receipt != null) return receipt;
            }

            return null;
        }

        public async Task<IncludedUserOperation?> GetUserOperationByHashAsync(string userOpHash)
        {
            var entry = await _mempool.GetAsync(userOpHash);
            if (entry == null) return null;

            if (entry.State == MempoolEntryState.Included &&
                entry.BlockHash == null &&
                entry.TransactionHash != null)
            {
                var txReceipt = await _web3.Eth.Transactions.GetTransactionReceipt
                    .SendRequestAsync(entry.TransactionHash);
                if (txReceipt != null)
                {
                    await _mempool.MarkIncludedAsync(
                        new[] { entry.UserOpHash },
                        entry.TransactionHash,
                        txReceipt.BlockNumber?.Value ?? entry.BlockNumber ?? 0,
                        txReceipt.BlockHash);
                    entry.BlockNumber = txReceipt.BlockNumber?.Value ?? entry.BlockNumber;
                    entry.BlockHash = txReceipt.BlockHash;
                }
            }

            return new IncludedUserOperation
            {
                UserOpHash = entry.UserOpHash,
                UserOperation = entry.UserOperation,
                EntryPoint = entry.EntryPoint,
                TransactionHash = entry.TransactionHash,
                BlockNumber = entry.BlockNumber ?? 0,
                BlockHash = entry.BlockHash
            };
        }

        public Task<string[]> SupportedEntryPointsAsync()
        {
            return Task.FromResult(_config.SupportedEntryPoints);
        }

        public async Task<BigInteger> ChainIdAsync()
        {
            if (_chainId.HasValue) return _chainId.Value;

            if (_config.ChainId.HasValue)
            {
                _chainId = _config.ChainId.Value;
            }
            else
            {
                _chainId = await _web3.Eth.ChainId.SendRequestAsync();
            }

            return _chainId.Value;
        }

        public async Task<UserOperationStatus> GetUserOperationStatusAsync(string userOpHash)
        {
            var entry = await _mempool.GetAsync(userOpHash);
            if (entry == null)
            {
                return new UserOperationStatus
                {
                    UserOpHash = userOpHash,
                    State = UserOpState.Dropped,
                    Error = "Not found"
                };
            }

            return new UserOperationStatus
            {
                UserOpHash = entry.UserOpHash,
                State = entry.State switch
                {
                    MempoolEntryState.Pending => UserOpState.Pending,
                    MempoolEntryState.Submitted => UserOpState.Submitted,
                    MempoolEntryState.Included => UserOpState.Included,
                    MempoolEntryState.Failed => UserOpState.Failed,
                    _ => UserOpState.Dropped
                },
                TransactionHash = entry.TransactionHash,
                Error = entry.Error,
                SubmittedAt = entry.SubmittedAt
            };
        }

        public async Task<PendingUserOperation[]> GetPendingUserOperationsAsync()
        {
            var entries = await _mempool.GetAllPendingAsync();
            return entries.Select(e => new PendingUserOperation
            {
                UserOpHash = e.UserOpHash,
                UserOperation = e.UserOperation,
                EntryPoint = e.EntryPoint,
                SubmittedAt = e.SubmittedAt,
                RetryCount = e.RetryCount
            }).ToArray();
        }

        public async Task<bool> DropUserOperationAsync(string userOpHash)
        {
            return await _mempool.RemoveAsync(userOpHash);
        }

        public async Task<string?> FlushAsync()
        {
            await _bundleGate.WaitAsync();
            try
            {
                var result = await ExecuteBundleCoreAsync(minBaseFee: null);
                return result?.TransactionHash;
            }
            finally
            {
                _bundleGate.Release();
            }
        }

        public async Task<BundlerStats> GetStatsAsync()
        {
            var mempoolStats = await _mempool.GetStatsAsync();

            return new BundlerStats
            {
                PendingCount = mempoolStats.PendingCount,
                SubmittedCount = mempoolStats.SubmittedCount,
                IncludedCount = _stats.IncludedCount,
                FailedCount = _stats.FailedCount,
                BundlesSubmitted = _stats.BundlesSubmitted,
                TotalGasUsed = _stats.TotalGasUsed,
                StartedAt = _stats.StartedAt
            };
        }

        public async Task SetReputationAsync(string address, ReputationEntry reputation)
        {
            if (_reputationService != null)
            {
                await _reputationService.UpdateAsync(reputation);
            }
            else
            {
                _inMemoryReputation[address.ToLowerInvariant()] = reputation;
            }
        }

        public async Task<ReputationEntry> GetReputationAsync(string address)
        {
            if (_reputationService != null)
            {
                var entry = await _reputationService.GetAsync(address);
                if (entry != null)
                {
                    return entry;
                }
            }
            else if (_inMemoryReputation.TryGetValue(address.ToLowerInvariant(), out var inMemEntry))
            {
                return inMemEntry;
            }

            return new ReputationEntry
            {
                Address = address,
                Status = ReputationStatus.Ok
            };
        }

        public async Task<ReputationEntry[]> GetAllReputationAsync()
        {
            if (_reputationService != null)
            {
                return await _reputationService.GetAllAsync();
            }

            return _inMemoryReputation.Values.ToArray();
        }

        public async Task<StakeStatus> GetStakeStatusAsync(string address, string entryPoint)
        {
            var info = await _stakingInfo.GetEntityAsync(
                address, Validation.ERC7562.EntityType.None, entryPoint);

            return new StakeStatus
            {
                Address = address,
                Stake = info.StakeAmount,
                UnstakeDelaySec = info.UnstakeDelaySec,
                IsStaked = info.IsStaked
            };
        }

        public void SetBundlingMode(BundlingMode mode)
        {
            _bundlingMode = mode;

            if (_autoBundleTimer == null) return;

            if (mode == BundlingMode.Manual)
            {
                _autoBundleTimer.Change(Timeout.Infinite, Timeout.Infinite);
            }
            else
            {
                _autoBundleTimer.Change(_config.AutoBundleIntervalMs, _config.AutoBundleIntervalMs);
            }
        }

        public async Task ClearStateAsync()
        {
            await _mempool.ClearAsync();
            await ClearReputationAsync();
            _receipts.Clear();

            _stats.PendingCount = 0;
            _stats.SubmittedCount = 0;
            _stats.IncludedCount = 0;
            _stats.FailedCount = 0;
            _stats.BundlesSubmitted = 0;
            _stats.TotalGasUsed = 0;
        }

        public Task ClearMempoolAsync()
        {
            return _mempool.ClearAsync();
        }

        public async Task ClearReputationAsync()
        {
            if (_reputationService != null)
            {
                await _reputationService.ClearAllAsync();
            }

            _inMemoryReputation.Clear();
        }

        public async Task<BundleExecutionResult?> ExecuteBundleAsync()
        {
            await _bundleGate.WaitAsync();
            try
            {
                return await ExecuteBundleCoreAsync(minBaseFee: null);
            }
            finally
            {
                _bundleGate.Release();
            }
        }

        public async Task<BundleExecutionResult?> ExecuteBundleAsync(BigInteger? minBaseFee)
        {
            await _bundleGate.WaitAsync();
            try
            {
                return await ExecuteBundleCoreAsync(minBaseFee);
            }
            finally
            {
                _bundleGate.Release();
            }
        }

        private async Task<BundleExecutionResult?> ExecuteBundleCoreAsync(BigInteger? minBaseFee)
        {
            var pending = await _mempool.GetPendingAsync(_config.MaxBundleSize, _config.MaxBundleGas);

            pending = await AnchorToOnChainNonceAsync(pending);

            pending = FilterBundleCandidates(pending, await _mempool.GetAllPendingAsync(), minBaseFee);

            BundleExecutionResult? lastEvictionResult = null;
            var attemptSize = pending.Length;

            while (pending.Length > 0)
            {
                attemptSize = Math.Min(attemptSize, pending.Length);
                var attempt = pending.Take(attemptSize).ToArray();

                var bundle = await _executor.BuildBundleAsync(attempt);
                var hashes = bundle.UserOpHashes;

                string transactionHash;
                try
                {
                    transactionHash = await _executor.SubmitAsync(bundle);
                }
                catch (BundleFailedOpException ex)
                {
                    var submissionOrdered = bundle.SubmissionOrderedEntries;

                    if (ex.OpIndex < 0 || ex.OpIndex >= submissionOrdered.Length)
                    {
                        var attemptHashes = attempt.Select(e => e.UserOpHash).ToArray();
                        var error = $"FailedOp opIndex {ex.OpIndex} is out of range for a bundle of " +
                                    $"{submissionOrdered.Length} operations; failing the whole attempt: {ex.Message}";

                        await _mempool.MarkFailedAsync(attemptHashes, error);
                        _stats.FailedCount += attemptHashes.Length;

                        lastEvictionResult = BundleExecutionResult.Failed(error);

                        var failedChainKeys = attempt.Select(ChainKeyOf).ToHashSet();
                        pending = pending
                            .Where(e => !attemptHashes.Contains(e.UserOpHash))
                            .Where(e => !failedChainKeys.Contains(ChainKeyOf(e)))
                            .ToArray();
                        attemptSize = pending.Length;
                        continue;
                    }

                    var offender = submissionOrdered[ex.OpIndex];
                    await _mempool.MarkFailedAsync(new[] { offender.UserOpHash }, ex.Message);
                    _stats.FailedCount++;

                    if (_reputationService != null)
                    {
                        await RecordFailedOpReputationAsync(offender, ex.Reason);
                    }

                    lastEvictionResult = new BundleExecutionResult
                    {
                        Success = false,
                        Error = ex.Message,
                        FailedOpIndex = ex.OpIndex,
                        FailedOpReason = ex.Reason
                    };

                    pending = RemoveEvictedAndOrphanedSuccessors(pending, offender);
                    attemptSize = pending.Length;
                    continue;
                }
                catch (BundleSimulationRevertedException) when (attempt.Length > 1)
                {
                    attemptSize = attempt.Length / 2;
                    continue;
                }
                catch (BundleSimulationRevertedException ex)
                {
                    await _mempool.MarkFailedAsync(new[] { attempt[0].UserOpHash }, ex.Message);
                    _stats.FailedCount++;

                    lastEvictionResult = BundleExecutionResult.Failed(ex.Message);

                    pending = RemoveEvictedAndOrphanedSuccessors(pending, attempt[0]);
                    attemptSize = pending.Length;
                    continue;
                }
                catch (Exception ex)
                {
                    return BundleExecutionResult.Failed($"Bundle submission failed: {ex.Message}");
                }

                await _mempool.MarkSubmittedAsync(hashes, transactionHash);

                var result = await _executor.WaitForBundleReceiptAsync(bundle, transactionHash);

                if (result.Success)
                {
                    var eventless = result.UserOpResults
                        .Where(r => !r.EventFound)
                        .Select(r => r.UserOpHash)
                        .ToArray();
                    var included = hashes.Except(eventless).ToArray();

                    await _mempool.MarkIncludedAsync(
                        included,
                        transactionHash,
                        result.Receipt?.BlockNumber?.Value ?? 0,
                        result.Receipt?.BlockHash);

                    if (eventless.Length > 0)
                    {
                        await _mempool.MarkFailedAsync(
                            eventless,
                            "No UserOperationEvent emitted for this operation in the bundle transaction");
                        _stats.FailedCount += eventless.Length;
                    }

                    _stats.BundlesSubmitted++;
                    _stats.IncludedCount += included.Length;
                    _stats.TotalGasUsed += result.GasUsed;

                    if (_reputationService != null)
                    {
                        foreach (var entry in bundle.Entries.Where(e => included.Contains(e.UserOpHash)))
                        {
                            await RecordIncludedReputationAsync(entry);
                        }
                    }
                }
                else if (result.ReceiptTimedOut)
                {
                    await _mempool.RevertSubmittedAsync(transactionHash);
                }
                else
                {
                    await _mempool.MarkFailedAsync(hashes, result.Error ?? "Bundle transaction reverted");
                    _stats.FailedCount += hashes.Length;
                }

                return result;
            }

            return lastEvictionResult;
        }

        private async Task<MempoolEntry[]> AnchorToOnChainNonceAsync(MempoolEntry[] candidates)
        {
            if (candidates.Length == 0) return candidates;

            var groups = candidates.GroupBy(MempoolChainKey.Of);

            var onChainNonceCache = new Dictionary<ChainKey, BigInteger>();
            var result = new List<MempoolEntry>();

            foreach (var group in groups)
            {
                var (sender, key, entryPoint) = group.Key;
                if (string.IsNullOrEmpty(sender)) continue;

                if (!_entryPoints.TryGetValue(entryPoint, out var epService))
                {
                    continue;
                }

                if (!onChainNonceCache.TryGetValue(group.Key, out var onChainNonce))
                {
                    onChainNonce = await epService.GetNonceQueryAsync(sender, key);
                    onChainNonceCache[group.Key] = onChainNonce;
                }

                var survivors = group.Where(e => e.UserOperation.Nonce >= onChainNonce).ToArray();
                if (survivors.Length == 0)
                {
                    foreach (var stale in group)
                    {
                        await _mempool.RemoveAsync(stale.UserOpHash);
                    }
                    continue;
                }
                if (survivors[0].UserOperation.Nonce != onChainNonce) continue;

                result.AddRange(survivors);
            }

            return result.ToArray();
        }

        private async Task CheckPaymasterDepositAsync(MempoolEntry entry, string entryPoint, MempoolEntry? replacedEntry)
        {
            if (string.IsNullOrEmpty(entry.Paymaster)) return;

            var epService = _entryPoints[entryPoint.ToLowerInvariant()];
            var deposit = await epService.BalanceOfQueryAsync(entry.Paymaster);

            var required = entry.Prefund;
            foreach (var pending in await _mempool.GetAllPendingAsync())
            {
                if (replacedEntry != null && pending.UserOpHash == replacedEntry.UserOpHash) continue;

                if (!string.IsNullOrEmpty(pending.Paymaster) &&
                    pending.Paymaster.IsTheSameAddress(entry.Paymaster) &&
                    pending.EntryPoint.Equals(entryPoint, StringComparison.OrdinalIgnoreCase))
                {
                    required += pending.Prefund;
                }
            }

            if (required > deposit)
            {
                throw new BundlerRpcException(
                    BundlerErrorCodes.PaymasterDepositTooLow,
                    $"paymaster deposit too low for all mempool UserOps - required {required}, available {deposit}",
                    new { paymaster = entry.Paymaster });
            }
        }

        private async Task CheckSenderPrefundAsync(MempoolEntry entry, string entryPoint, MempoolEntry? replacedEntry)
        {
            if (!string.IsNullOrEmpty(entry.Paymaster)) return;

            var sender = entry.UserOperation.Sender;
            if (string.IsNullOrEmpty(sender)) return;

            var epService = _entryPoints[entryPoint.ToLowerInvariant()];
            var deposit = await epService.BalanceOfQueryAsync(sender);
            var balance = (await _web3.Eth.GetBalance.SendRequestAsync(sender)).Value;
            var available = deposit + balance;

            var nonceKey = entry.UserOperation.Nonce >> 64;
            var required = entry.Prefund;

            foreach (var pending in await _mempool.GetAllPendingAsync())
            {
                if (replacedEntry != null && pending.UserOpHash == replacedEntry.UserOpHash) continue;

                if (string.IsNullOrEmpty(pending.Paymaster) &&
                    !string.IsNullOrEmpty(pending.UserOperation.Sender) &&
                    pending.UserOperation.Sender.IsTheSameAddress(sender) &&
                    (pending.UserOperation.Nonce >> 64) == nonceKey &&
                    pending.EntryPoint.Equals(entryPoint, StringComparison.OrdinalIgnoreCase))
                {
                    required += pending.Prefund;
                }
            }

            if (required > available)
            {
                throw new BundlerRpcException(
                    BundlerErrorCodes.SimulateValidation,
                    $"AA21: sender didn't pay prefund for all mempool UserOps - required {required}, available {available}",
                    new { sender });
            }
        }

        private async Task CheckSenderMempoolLimitAsync(string? sender, string entryPoint)
        {
            if (string.IsNullOrEmpty(sender)) return;
            if (_config.WhitelistedAddresses.Contains(sender.ToLowerInvariant())) return;

            var activeCount = (await _mempool.GetBySenderAsync(sender))
                .Count(e => e.State == MempoolEntryState.Pending);

            if (activeCount < _config.MaxUnstakedSenderMempoolCount) return;

            if (await _stakingInfo.IsStakedAsync(sender, entryPoint)) return;

            throw new BundlerRpcException(
                BundlerErrorCodes.InsufficientStake,
                $"Sender {sender} already has {activeCount} UserOperations in the mempool; " +
                $"an unstaked sender is limited to {_config.MaxUnstakedSenderMempoolCount}",
                new { sender });
        }

        private static MempoolEntry[] FilterBundleCandidates(
            MempoolEntry[] candidates, MempoolEntry[] allPending, BigInteger? minBaseFee)
        {
            var knownSenders = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in allPending)
            {
                if (!string.IsNullOrEmpty(entry.UserOperation.Sender))
                {
                    knownSenders.Add(entry.UserOperation.Sender);
                }
            }

            bool IsUnderpriced(MempoolEntry entry) =>
                minBaseFee.HasValue && minBaseFee.Value > 0 &&
                entry.UserOperation.UnpackGasFees().MaxFeePerGas < minBaseFee.Value;

            bool HasStorageConflict(MempoolEntry entry)
            {
                var sender = entry.UserOperation.Sender ?? "";
                foreach (var storageAddress in entry.AccessedStorageAddresses)
                {
                    if (!storageAddress.Equals(sender, StringComparison.OrdinalIgnoreCase) &&
                        knownSenders.Contains(storageAddress))
                    {
                        return true;
                    }
                }
                return false;
            }

            var brokenChains = new HashSet<ChainKey>();
            var result = new List<MempoolEntry>();

            foreach (var entry in candidates)
            {
                var chainKey = ChainKeyOf(entry);
                if (brokenChains.Contains(chainKey)) continue;

                if (IsUnderpriced(entry) || HasStorageConflict(entry))
                {
                    brokenChains.Add(chainKey);
                    continue;
                }

                result.Add(entry);
            }

            return result.ToArray();
        }

        private static MempoolEntry[] RemoveEvictedAndOrphanedSuccessors(MempoolEntry[] pending, MempoolEntry evicted)
        {
            var chainKey = ChainKeyOf(evicted);

            return pending
                .Where(e => e.UserOpHash != evicted.UserOpHash)
                .Where(e => !(ChainKeyOf(e) == chainKey && e.UserOperation.Nonce > evicted.UserOperation.Nonce))
                .ToArray();
        }

        private static ChainKey ChainKeyOf(MempoolEntry entry) => MempoolChainKey.Of(entry);

        private async Task CheckMultipleRolesViolationAsync(MempoolEntry entry)
        {
            if (!_config.EnableERC7562Validation) return;

            var pending = await _mempool.GetAllPendingAsync();
            var violation = MultipleRolesRule.Detect(
                entry.UserOperation.Sender, entry.Paymaster, entry.Factory, pending);

            if (violation != null)
            {
                throw new BundlerRpcException(BundlerErrorCodes.OpcodeValidation, violation);
            }
        }

        private async Task CheckReputationAsync(PackedUserOperation userOp)
        {
            var sender = userOp.Sender?.ToLowerInvariant() ?? "";

            if (!_config.WhitelistedAddresses.Contains(sender))
            {
                if (await _reputationService!.IsBannedAsync(sender))
                {
                    throw new BundlerRpcException(
                        BundlerErrorCodes.Reputation, $"Sender {sender} is banned", new { sender });
                }

                if (await _reputationService.IsThrottledAsync(sender))
                {
                    throw new BundlerRpcException(
                        BundlerErrorCodes.Reputation, $"Sender {sender} is throttled", new { sender });
                }
            }

            var factory = ExtractFactory(userOp.InitCode);
            if (!string.IsNullOrEmpty(factory) && !_config.WhitelistedAddresses.Contains(factory.ToLowerInvariant()))
            {
                if (await _reputationService!.IsBannedAsync(factory))
                {
                    throw new BundlerRpcException(
                        BundlerErrorCodes.Reputation, $"Factory {factory} is banned", new { factory });
                }

                if (await _reputationService.IsThrottledAsync(factory))
                {
                    throw new BundlerRpcException(
                        BundlerErrorCodes.Reputation, $"Factory {factory} is throttled", new { factory });
                }
            }

            var paymaster = ExtractPaymaster(userOp.PaymasterAndData);
            if (!string.IsNullOrEmpty(paymaster) && !_config.WhitelistedAddresses.Contains(paymaster.ToLowerInvariant()))
            {
                if (await _reputationService!.IsBannedAsync(paymaster))
                {
                    throw new BundlerRpcException(
                        BundlerErrorCodes.Reputation, $"Paymaster {paymaster} is banned", new { paymaster });
                }

                if (await _reputationService.IsThrottledAsync(paymaster))
                {
                    throw new BundlerRpcException(
                        BundlerErrorCodes.Reputation, $"Paymaster {paymaster} is throttled", new { paymaster });
                }
            }
        }

        private async Task RecordIncludedReputationAsync(MempoolEntry entry)
        {
            await _reputationService!.RecordIncludedAsync(entry.UserOperation.Sender ?? "");

            if (!string.IsNullOrEmpty(entry.Factory))
            {
                await _reputationService.RecordIncludedAsync(entry.Factory);
            }

            if (!string.IsNullOrEmpty(entry.Paymaster))
            {
                await _reputationService.RecordIncludedAsync(entry.Paymaster);
            }
        }

        private async Task RecordSeenReputationAsync(MempoolEntry entry, string entryPoint)
        {
            var sender = entry.UserOperation.Sender;
            if (!string.IsNullOrEmpty(sender) && await _stakingInfo.IsStakedAsync(sender, entryPoint))
            {
                await _reputationService!.RecordSeenAsync(sender, 1);
            }

            if (!string.IsNullOrEmpty(entry.Factory))
            {
                await _reputationService!.RecordSeenAsync(entry.Factory, 1);
            }

            if (!string.IsNullOrEmpty(entry.Paymaster))
            {
                await _reputationService!.RecordSeenAsync(entry.Paymaster, 1);
            }
        }

        private async Task RevertSeenReputationAsync(MempoolEntry entry, string entryPoint)
        {
            var sender = entry.UserOperation.Sender;
            if (!string.IsNullOrEmpty(sender) && await _stakingInfo.IsStakedAsync(sender, entryPoint))
            {
                await _reputationService!.RecordSeenAsync(sender, -1);
            }

            if (!string.IsNullOrEmpty(entry.Factory))
            {
                await _reputationService!.RecordSeenAsync(entry.Factory, -1);
            }

            if (!string.IsNullOrEmpty(entry.Paymaster))
            {
                await _reputationService!.RecordSeenAsync(entry.Paymaster, -1);
            }
        }

        private async Task RecordFailedOpReputationAsync(MempoolEntry entry, string reason)
        {
            if (reason.StartsWith("AA25")) return;

            var entryPoint = entry.EntryPoint;
            var sender = entry.UserOperation.Sender;

            var isSenderStaked = !string.IsNullOrEmpty(sender) && await _stakingInfo.IsStakedAsync(sender, entryPoint);
            var isFactoryStaked = !string.IsNullOrEmpty(entry.Factory) && await _stakingInfo.IsStakedAsync(entry.Factory, entryPoint);

            var blame = FailedOpBlameResolver.Resolve(reason, sender, entry.Factory, entry.Paymaster, isSenderStaked, isFactoryStaked);
            var blamedAddress = blame.BlamedAddress;
            if (string.IsNullOrEmpty(blamedAddress)) return;

            await RevertSeenReputationAsync(entry, entryPoint);

            if (blame.IsStakedAccountabilityPenalty)
            {
                await _reputationService!.ApplyStakedAccountabilityPenaltyAsync(blamedAddress);
            }
            else
            {
                await _reputationService!.RecordSeenAsync(blamedAddress, 1);
            }

            await _reputationService!.RecordFailedAsync(blamedAddress);
        }

        private void AutoBundleCallback(object? state)
        {
            if (_bundlingMode == BundlingMode.Manual) return;

            if (!_bundleGate.Wait(0)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    var minBaseFee = await GetAutoBundleBaseFeeFloorAsync();
                    await ExecuteBundleCoreAsync(minBaseFee);
                    await _mempool.PruneAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Auto-bundle tick failed.");
                }
                finally
                {
                    _bundleGate.Release();
                }
            });
        }

        private void ReputationDecayCallback(object? state)
        {
            if (_reputationService == null) return;

            if (!_reputationDecayGate.Wait(0)) return;

            _ = Task.Run(async () =>
            {
                try
                {
                    await _reputationService.DecayAsync();
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "Reputation decay tick failed.");
                }
                finally
                {
                    _reputationDecayGate.Release();
                }
            });
        }

        private async Task<BigInteger?> GetAutoBundleBaseFeeFloorAsync()
        {
            if (!_config.SkipUnderpricedOpsInAutoBundle) return null;

            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsHashesByNumber
                .SendRequestAsync(BlockParameter.CreateLatest());

            return block?.BaseFeePerGas?.Value;
        }

        private async Task<string> CalculateUserOpHashAsync(
            PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null)
        {
            var epService = _entryPoints[entryPoint.ToLowerInvariant()];

            // EIP-7702: for a factory=0x7702 op the v0.9 EntryPoint.getUserOpHash reads the sender's
            // revert ("sender has no code"). Compute the hash via an eth_call that overrides the
            if (eip7702Auth != null && !string.IsNullOrEmpty(eip7702Auth.Address))
            {
                var function = new GetUserOpHashFunction { UserOp = userOp };
                var callInput = function.CreateTransactionInput(entryPoint);
                var stateOverride = new Dictionary<string, StateChange>
                {
                    [userOp.Sender] = new StateChange
                    {
                        Code = Eip7702DelegationUtils.CreateDelegationCode(eip7702Auth.Address).ToHex(true)
                    }
                };

                var raw = await new EthCall(_web3.Client).SendRequestAsync(
                    callInput, BlockParameter.CreateLatest(), stateOverride);
                var decoded = new FunctionCallDecoder()
                    .DecodeFunctionOutput(new GetUserOpHashOutputDTO(), raw);
                return decoded.ReturnValue1.ToHex(true);
            }

            var hash = await epService.GetUserOpHashQueryAsync(userOp);
            return hash.ToHex(true);
        }

        private void ValidateEntryPoint(string entryPoint)
        {
            if (!_entryPoints.ContainsKey(entryPoint.ToLowerInvariant()))
            {
                throw new BundlerRpcException(
                    BundlerErrorCodes.InvalidFields,
                    $"Unsupported EntryPoint: {entryPoint}");
            }
        }

        private static BigInteger CalculatePrefund(PackedUserOperation userOp)
        {
            var (_, maxFee) = userOp.UnpackGasFees();
            return userOp.GetTotalGas() * maxFee;
        }

        private static string? ExtractFactory(byte[]? initCode)
        {
            if (initCode == null || initCode.Length < 20
                || Nethereum.AccountAbstraction.AAEIP7702Utils.IsEip7702UserOp(initCode)) return null;
            return "0x" + initCode.Take(20).ToArray().ToHex();
        }

        private static string? ExtractPaymaster(byte[]? paymasterAndData)
        {
            if (paymasterAndData == null || paymasterAndData.Length < 20) return null;
            return "0x" + paymasterAndData.Take(20).ToArray().ToHex();
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _autoBundleTimer?.Dispose();
                _reputationDecayTimer?.Dispose();
                _admissionGate.Dispose();
                _bundleGate.Dispose();
                _reputationDecayGate.Dispose();
            }

            _disposed = true;
        }
    }
}
