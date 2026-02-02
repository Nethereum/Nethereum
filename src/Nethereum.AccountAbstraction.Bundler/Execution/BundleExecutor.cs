using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Aggregation;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Interfaces;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    /// <summary>
    /// Builds and executes bundles of UserOperations.
    /// </summary>
    public class BundleExecutor : IBundleExecutor
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();
        private readonly IAggregatorManager? _aggregatorManager;

        public BundleExecutor(IWeb3 web3, BundlerConfig config)
            : this(web3, config, null)
        {
        }

        public BundleExecutor(IWeb3 web3, BundlerConfig config, IAggregatorManager? aggregatorManager)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _aggregatorManager = aggregatorManager;

            foreach (var ep in config.SupportedEntryPoints)
            {
                _entryPoints[ep.ToLowerInvariant()] = new EntryPointService(web3, ep);
            }
        }

        public async Task<Bundle> BuildBundleAsync(MempoolEntry[] entries)
        {
            if (entries == null || entries.Length == 0)
            {
                throw new ArgumentException("No entries to bundle");
            }

            var entryPoint = entries[0].EntryPoint;
            if (!entries.All(e => e.EntryPoint.Equals(entryPoint, StringComparison.OrdinalIgnoreCase)))
            {
                throw new ArgumentException("All entries must use the same EntryPoint");
            }

            BigInteger estimatedGas = 0;
            foreach (var entry in entries)
            {
                estimatedGas += GetOperationGas(entry);
            }

            estimatedGas += 21000;
            estimatedGas += (BigInteger)(entries.Length * 5000);

            var bundle = new Bundle
            {
                Entries = entries,
                EntryPoint = entryPoint,
                Beneficiary = _config.BeneficiaryAddress,
                EstimatedGas = estimatedGas,
                CreatedAt = DateTimeOffset.UtcNow
            };

            if (_aggregatorManager != null && _aggregatorManager.SupportsAggregation)
            {
                bundle.AggregatedGroups = await GroupByAggregatorAsync(entries);
            }

            return bundle;
        }

        private async Task<Dictionary<string, AggregatedGroup>> GroupByAggregatorAsync(MempoolEntry[] entries)
        {
            var groups = new Dictionary<string, List<MempoolEntry>>();

            foreach (var entry in entries)
            {
                var aggregatorAddress = _aggregatorManager!.DetectAggregator(entry.UserOperation);
                if (!string.IsNullOrEmpty(aggregatorAddress))
                {
                    if (!groups.ContainsKey(aggregatorAddress))
                    {
                        groups[aggregatorAddress] = new List<MempoolEntry>();
                    }
                    groups[aggregatorAddress].Add(entry);
                }
            }

            var result = new Dictionary<string, AggregatedGroup>();

            foreach (var (aggregatorAddress, groupEntries) in groups)
            {
                if (groupEntries.Count < 2)
                    continue;

                var aggregator = _aggregatorManager!.GetAggregator(aggregatorAddress);
                if (aggregator == null)
                    continue;

                try
                {
                    var userOps = groupEntries.Select(e => e.UserOperation).ToArray();
                    var aggregatedSig = await aggregator.AggregateSignaturesAsync(userOps);

                    result[aggregatorAddress] = new AggregatedGroup
                    {
                        Aggregator = aggregatorAddress,
                        Entries = groupEntries.ToArray(),
                        AggregatedSignature = aggregatedSig
                    };
                }
                catch
                {
                }
            }

            return result;
        }

        public async Task<BundleExecutionResult> ExecuteAsync(Bundle bundle)
        {
            if (bundle.Entries.Length == 0)
            {
                return BundleExecutionResult.Failed("Empty bundle");
            }

            var epService = GetEntryPointService(bundle.EntryPoint);
            if (epService == null)
            {
                return BundleExecutionResult.Failed($"Unsupported EntryPoint: {bundle.EntryPoint}");
            }

            try
            {
                TransactionReceipt receipt;

                if (bundle.UsesAggregation)
                {
                    receipt = await ExecuteAggregatedAsync(bundle, epService);
                }
                else
                {
                    receipt = await ExecuteStandardAsync(bundle, epService);
                }

                if (receipt.Status?.Value != 1)
                {
                    return BundleExecutionResult.Failed($"Transaction reverted: {receipt.TransactionHash}");
                }

                var userOpResults = ParseUserOpEvents(receipt, bundle);

                return new BundleExecutionResult
                {
                    Success = true,
                    TransactionHash = receipt.TransactionHash,
                    Receipt = receipt,
                    GasUsed = receipt.GasUsed?.Value ?? 0,
                    UserOpResults = userOpResults
                };
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                var errorMessage = ParseEntryPointError(ex);
                return BundleExecutionResult.Failed(errorMessage);
            }
            catch (Exception ex)
            {
                return BundleExecutionResult.Failed($"Execution error: {ex.Message}");
            }
        }

        private async Task<TransactionReceipt> ExecuteStandardAsync(Bundle bundle, EntryPointService epService)
        {
            var ops = bundle.Entries
                .Select(e => ConvertToContractUserOp(e.UserOperation))
                .ToList();

            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = ops,
                Beneficiary = bundle.Beneficiary,
                Gas = bundle.EstimatedGas
            };

            return await epService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);
        }

        private async Task<TransactionReceipt> ExecuteAggregatedAsync(Bundle bundle, EntryPointService epService)
        {
            var opsPerAggregator = new List<UserOpsPerAggregator>();

            foreach (var (aggregatorAddress, group) in bundle.AggregatedGroups)
            {
                var userOps = group.Entries
                    .Select(e => ConvertToContractUserOp(e.UserOperation))
                    .ToList();

                opsPerAggregator.Add(new UserOpsPerAggregator
                {
                    UserOps = userOps,
                    Aggregator = aggregatorAddress,
                    Signature = group.AggregatedSignature
                });
            }

            var nonAggregatedEntries = bundle.NonAggregatedEntries;
            if (nonAggregatedEntries.Length > 0)
            {
                var nonAggregatedOps = nonAggregatedEntries
                    .Select(e => ConvertToContractUserOp(e.UserOperation))
                    .ToList();

                opsPerAggregator.Add(new UserOpsPerAggregator
                {
                    UserOps = nonAggregatedOps,
                    Aggregator = "0x0000000000000000000000000000000000000000",
                    Signature = Array.Empty<byte>()
                });
            }

            var handleAggregatedOpsFunction = new HandleAggregatedOpsFunction
            {
                OpsPerAggregator = opsPerAggregator,
                Beneficiary = bundle.Beneficiary,
                Gas = bundle.EstimatedGas
            };

            return await epService.HandleAggregatedOpsRequestAndWaitForReceiptAsync(handleAggregatedOpsFunction);
        }

        public async Task<BigInteger> EstimateBundleGasAsync(Bundle bundle)
        {
            if (bundle.Entries.Length == 0)
            {
                return 0;
            }

            var epService = GetEntryPointService(bundle.EntryPoint);
            if (epService == null)
            {
                throw new ArgumentException($"Unsupported EntryPoint: {bundle.EntryPoint}");
            }

            try
            {
                var ops = bundle.Entries
                    .Select(e => ConvertToContractUserOp(e.UserOperation))
                    .ToList();

                var handleOpsFunction = new HandleOpsFunction
                {
                    Ops = ops,
                    Beneficiary = bundle.Beneficiary
                };

                var callInput = handleOpsFunction.CreateCallInput(epService.ContractAddress);
                var estimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(callInput);

                return estimate.Value;
            }
            catch
            {
                return bundle.EstimatedGas;
            }
        }

        private EntryPointService? GetEntryPointService(string entryPoint)
        {
            _entryPoints.TryGetValue(entryPoint.ToLowerInvariant(), out var service);
            return service;
        }

        private static PackedUserOperation ConvertToContractUserOp(PackedUserOperation userOp)
        {
            return new PackedUserOperation
            {
                Sender = userOp.Sender,
                Nonce = userOp.Nonce,
                InitCode = userOp.InitCode ?? Array.Empty<byte>(),
                CallData = userOp.CallData ?? Array.Empty<byte>(),
                AccountGasLimits = userOp.AccountGasLimits ?? new byte[32],
                PreVerificationGas = userOp.PreVerificationGas,
                GasFees = userOp.GasFees ?? new byte[32],
                PaymasterAndData = userOp.PaymasterAndData ?? Array.Empty<byte>(),
                Signature = userOp.Signature ?? Array.Empty<byte>()
            };
        }

        private static UserOpExecutionResult[] ParseUserOpEvents(TransactionReceipt receipt, Bundle bundle)
        {
            var results = new List<UserOpExecutionResult>();

            try
            {
                var userOpEvents = receipt.Logs.DecodeAllEvents<UserOperationEventEventDTO>();

                foreach (var entry in bundle.Entries)
                {
                    var matchingEvent = userOpEvents.FirstOrDefault(e =>
                        e.Event.UserOpHash?.ToHex().Equals(entry.UserOpHash.Replace("0x", ""), StringComparison.OrdinalIgnoreCase) == true);

                    if (matchingEvent != null)
                    {
                        results.Add(new UserOpExecutionResult
                        {
                            UserOpHash = entry.UserOpHash,
                            Success = matchingEvent.Event.Success,
                            ActualGasUsed = matchingEvent.Event.ActualGasUsed,
                            ActualGasCost = matchingEvent.Event.ActualGasCost
                        });
                    }
                    else
                    {
                        results.Add(new UserOpExecutionResult
                        {
                            UserOpHash = entry.UserOpHash,
                            Success = true
                        });
                    }
                }

                var revertEvents = receipt.Logs.DecodeAllEvents<UserOperationRevertReasonEventDTO>();
                foreach (var revert in revertEvents)
                {
                    var hash = revert.Event.UserOpHash?.ToHex();
                    var result = results.FirstOrDefault(r => r.UserOpHash.Replace("0x", "").Equals(hash, StringComparison.OrdinalIgnoreCase));
                    if (result != null)
                    {
                        result.Success = false;
                        result.Error = revert.Event.RevertReason?.ToHex() ?? "Reverted";
                    }
                }
            }
            catch
            {
                foreach (var entry in bundle.Entries)
                {
                    results.Add(new UserOpExecutionResult
                    {
                        UserOpHash = entry.UserOpHash,
                        Success = true
                    });
                }
            }

            return results.ToArray();
        }

        private static string ParseEntryPointError(SmartContractCustomErrorRevertException ex)
        {
            if (ex.IsCustomErrorFor<FailedOpError>())
            {
                var error = ex.DecodeError<FailedOpError>();
                return $"FailedOp: opIndex={error.OpIndex}, reason={error.Reason}";
            }

            if (ex.IsCustomErrorFor<FailedOpWithRevertError>())
            {
                var error = ex.DecodeError<FailedOpWithRevertError>();
                return $"FailedOpWithRevert: opIndex={error.OpIndex}, reason={error.Reason}";
            }

            return ex.Message;
        }

        private static BigInteger GetOperationGas(MempoolEntry entry)
        {
            var userOp = entry.UserOperation;
            var accountGasLimits = userOp.AccountGasLimits ?? Array.Empty<byte>();

            BigInteger verificationGas = 0;
            BigInteger callGas = 0;

            if (accountGasLimits.Length >= 32)
            {
                verificationGas = new BigInteger(accountGasLimits.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
                callGas = new BigInteger(accountGasLimits.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            }

            return verificationGas + callGas + userOp.PreVerificationGas;
        }
    }
}
