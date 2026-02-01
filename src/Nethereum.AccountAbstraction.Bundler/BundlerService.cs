using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.GasEstimation;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler
{
    /// <summary>
    /// Main bundler service implementation.
    /// Processes UserOperations, manages the mempool, and executes bundles.
    /// </summary>
    public class BundlerService : IBundlerServiceExtended, IDisposable
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly IUserOpMempool _mempool;
        private readonly IUserOpValidator _validator;
        private readonly IBundleExecutor _executor;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();

        private readonly Dictionary<string, UserOperationReceipt> _receipts = new();
        private readonly Dictionary<string, ReputationEntry> _reputation = new();
        private readonly BundlerStats _stats = new() { StartedAt = DateTimeOffset.UtcNow };

        private Timer? _autoBundleTimer;
        private BigInteger? _chainId;
        private bool _disposed;

        public BundlerService(IWeb3 web3, BundlerConfig config)
            : this(web3, config, null, null, null)
        {
        }

        public BundlerService(
            IWeb3 web3,
            BundlerConfig config,
            IUserOpMempool? mempool,
            IUserOpValidator? validator,
            IBundleExecutor? executor)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mempool = mempool ?? new InMemoryUserOpMempool(config.MaxMempoolSize);
            _validator = validator ?? new UserOpValidator(web3, config);
            _executor = executor ?? new BundleExecutor(web3, config);

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
        }

        public async Task<string> SendUserOperationAsync(PackedUserOperation userOp, string entryPoint)
        {
            ValidateEntryPoint(entryPoint);

            if (_config.BlacklistedAddresses.Contains(userOp.Sender?.ToLowerInvariant() ?? ""))
            {
                throw new InvalidOperationException("Sender is blacklisted");
            }

            var validationResult = await _validator.ValidateAsync(userOp, entryPoint);
            if (!validationResult.IsValid)
            {
                throw new InvalidOperationException($"Validation failed: {validationResult.Error}");
            }

            var userOpHash = await CalculateUserOpHashAsync(userOp, entryPoint);

            var (maxPriorityFee, _) = UnpackGasFees(userOp.GasFees ?? new byte[32]);

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
                ValidAfter = validationResult.ValidAfter > 0 ? validationResult.ValidAfter : null
            };

            var added = await _mempool.AddAsync(entry);
            if (!added)
            {
                throw new InvalidOperationException("Failed to add to mempool (duplicate or full)");
            }

            return userOpHash;
        }

        public async Task<UserOperationGasEstimate> EstimateUserOperationGasAsync(UserOperation userOp, string entryPoint)
        {
            ValidateEntryPoint(entryPoint);

            var gasEstimator = new UserOperationGasEstimator(_web3, entryPoint);
            var estimate = await gasEstimator.EstimateGasAsync(userOp);

            return new UserOperationGasEstimate
            {
                CallGasLimit = new Nethereum.Hex.HexTypes.HexBigInteger(estimate.CallGasLimit),
                VerificationGasLimit = new Nethereum.Hex.HexTypes.HexBigInteger(estimate.VerificationGasLimit),
                PreVerificationGas = new Nethereum.Hex.HexTypes.HexBigInteger(estimate.PreVerificationGas),
                MaxFeePerGas = new Nethereum.Hex.HexTypes.HexBigInteger(estimate.MaxFeePerGas),
                MaxPriorityFeePerGas = new Nethereum.Hex.HexTypes.HexBigInteger(estimate.MaxPriorityFeePerGas)
            };
        }

        public async Task<UserOperationReceipt?> GetUserOperationReceiptAsync(string userOpHash)
        {
            if (_receipts.TryGetValue(userOpHash, out var receipt))
            {
                return receipt;
            }

            var entry = await _mempool.GetAsync(userOpHash);
            if (entry?.State == MempoolEntryState.Included && entry.TransactionHash != null)
            {
                var txReceipt = await _web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(entry.TransactionHash);
                if (txReceipt != null)
                {
                    var userOpReceipt = new UserOperationReceipt
                    {
                        UserOpHash = userOpHash,
                        EntryPoint = entry.EntryPoint,
                        Sender = entry.UserOperation.Sender,
                        Nonce = new Nethereum.Hex.HexTypes.HexBigInteger(entry.UserOperation.Nonce),
                        Success = txReceipt.Status?.Value == 1,
                        Receipt = txReceipt
                    };

                    _receipts[userOpHash] = userOpReceipt;
                    return userOpReceipt;
                }
            }

            return null;
        }

        public async Task<UserOperationInfo?> GetUserOperationByHashAsync(string userOpHash)
        {
            var entry = await _mempool.GetAsync(userOpHash);
            if (entry == null) return null;

            return new UserOperationInfo
            {
                UserOpHash = entry.UserOpHash,
                UserOperation = entry.UserOperation,
                EntryPoint = entry.EntryPoint,
                TransactionHash = entry.TransactionHash,
                BlockNumber = entry.BlockNumber ?? 0
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
            var entries = await _mempool.GetPendingAsync(int.MaxValue);
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
            var result = await ExecuteBundleAsync();
            return result?.TransactionHash;
        }

        public Task<BundlerStats> GetStatsAsync()
        {
            return Task.FromResult(new BundlerStats
            {
                PendingCount = _stats.PendingCount,
                SubmittedCount = _stats.SubmittedCount,
                IncludedCount = _stats.IncludedCount,
                FailedCount = _stats.FailedCount,
                BundlesSubmitted = _stats.BundlesSubmitted,
                TotalGasUsed = _stats.TotalGasUsed,
                StartedAt = _stats.StartedAt
            });
        }

        public Task SetReputationAsync(string address, ReputationEntry reputation)
        {
            _reputation[address.ToLowerInvariant()] = reputation;
            return Task.CompletedTask;
        }

        public Task<ReputationEntry> GetReputationAsync(string address)
        {
            if (_reputation.TryGetValue(address.ToLowerInvariant(), out var entry))
            {
                return Task.FromResult(entry);
            }

            return Task.FromResult(new ReputationEntry
            {
                Address = address,
                Status = ReputationStatus.Ok
            });
        }

        public async Task<BundleExecutionResult?> ExecuteBundleAsync()
        {
            var pending = await _mempool.GetPendingAsync(_config.MaxBundleSize, _config.MaxBundleGas);
            if (pending.Length == 0) return null;

            var bundle = await _executor.BuildBundleAsync(pending);
            var hashes = bundle.UserOpHashes;

            await _mempool.MarkSubmittedAsync(hashes, "pending");

            var result = await _executor.ExecuteAsync(bundle);

            if (result.Success && result.TransactionHash != null)
            {
                await _mempool.MarkIncludedAsync(
                    hashes,
                    result.TransactionHash,
                    result.Receipt?.BlockNumber?.Value ?? 0);

                _stats.BundlesSubmitted++;
                _stats.IncludedCount += hashes.Length;
                _stats.TotalGasUsed += result.GasUsed;
            }
            else
            {
                await _mempool.MarkFailedAsync(hashes, result.Error ?? "Unknown error");
                _stats.FailedCount += hashes.Length;
            }

            return result;
        }

        private void AutoBundleCallback(object? state)
        {
            _ = Task.Run(async () =>
            {
                try
                {
                    await ExecuteBundleAsync();
                    await _mempool.PruneAsync();
                }
                catch
                {
                }
            });
        }

        private async Task<string> CalculateUserOpHashAsync(PackedUserOperation userOp, string entryPoint)
        {
            var epService = _entryPoints[entryPoint.ToLowerInvariant()];
            var hash = await epService.GetUserOpHashQueryAsync(userOp);
            return hash.ToHex(true);
        }

        private void ValidateEntryPoint(string entryPoint)
        {
            if (!_entryPoints.ContainsKey(entryPoint.ToLowerInvariant()))
            {
                throw new ArgumentException($"Unsupported EntryPoint: {entryPoint}");
            }
        }

        private static BigInteger CalculatePrefund(PackedUserOperation userOp)
        {
            var accountGasLimits = userOp.AccountGasLimits ?? new byte[32];
            var gasFees = userOp.GasFees ?? new byte[32];

            var (verificationGas, callGas) = UnpackAccountGasLimits(accountGasLimits);
            var (_, maxFee) = UnpackGasFees(gasFees);

            var requiredGas = verificationGas + callGas + userOp.PreVerificationGas;
            return requiredGas * maxFee;
        }

        private static string? ExtractFactory(byte[]? initCode)
        {
            if (initCode == null || initCode.Length < 20) return null;
            return "0x" + initCode.Take(20).ToArray().ToHex();
        }

        private static string? ExtractPaymaster(byte[]? paymasterAndData)
        {
            if (paymasterAndData == null || paymasterAndData.Length < 20) return null;
            return "0x" + paymasterAndData.Take(20).ToArray().ToHex();
        }

        private static (BigInteger verificationGas, BigInteger callGas) UnpackAccountGasLimits(byte[] accountGasLimits)
        {
            if (accountGasLimits.Length < 32) return (0, 0);

            var verificationGas = new BigInteger(accountGasLimits.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            var callGas = new BigInteger(accountGasLimits.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            return (verificationGas, callGas);
        }

        private static (BigInteger maxPriorityFee, BigInteger maxFee) UnpackGasFees(byte[] gasFees)
        {
            if (gasFees.Length < 32) return (0, 0);

            var maxPriorityFee = new BigInteger(gasFees.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            var maxFee = new BigInteger(gasFees.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            return (maxPriorityFee, maxFee);
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
            }

            _disposed = true;
        }
    }
}
