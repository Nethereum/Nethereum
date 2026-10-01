using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Aggregation;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Interfaces;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.EVM.Execution;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.RPC.TransactionReceipts;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Execution
{
    public class BundleExecutor : IBundleExecutor
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();
        private readonly IAggregatorRegistry? _aggregatorRegistry;

        private const int Eip7702PerAuthGasCost = 25000;

        private const int HandleOpsEntryPointOverheadPerOp = 200000;

        public BundleExecutor(IWeb3 web3, BundlerConfig config)
            : this(web3, config, null)
        {
        }

        public BundleExecutor(IWeb3 web3, BundlerConfig config, IAggregatorRegistry? aggregatorRegistry)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _aggregatorRegistry = aggregatorRegistry;

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
                estimatedGas += entry.UserOperation.GetTotalGas();
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

            if (_aggregatorRegistry != null && _aggregatorRegistry.SupportsAggregation)
            {
                bundle.AggregatedGroups = await GroupByAggregatorAsync(entries);
            }

            return bundle;
        }

        private async Task<Dictionary<string, AggregatedGroup>> GroupByAggregatorAsync(MempoolEntry[] entries)
        {
            var detectedByEntry = entries
                .Select(entry => (entry, aggregator: _aggregatorRegistry!.DetectAggregator(entry.UserOperation)))
                .ToArray();

            var chainAggregator = detectedByEntry
                .GroupBy(x => MempoolChainKey.Of(x.entry))
                .ToDictionary(chain => chain.Key, chain =>
                {
                    var distinct = chain.Select(x => x.aggregator).Distinct().ToArray();
                    return distinct.Length == 1 && !string.IsNullOrEmpty(distinct[0]) ? distinct[0] : null;
                });

            var groups = new Dictionary<string, List<MempoolEntry>>();

            foreach (var (entry, _) in detectedByEntry)
            {
                var aggregatorAddress = chainAggregator[MempoolChainKey.Of(entry)];
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

                var aggregator = _aggregatorRegistry!.GetAggregator(aggregatorAddress);
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
            string transactionHash;
            try
            {
                transactionHash = await SubmitAsync(bundle);
            }
            catch (BundleFailedOpException ex)
            {
                return new BundleExecutionResult
                {
                    Success = false,
                    Error = ex.Message,
                    FailedOpIndex = ex.OpIndex,
                    FailedOpReason = ex.Reason
                };
            }
            catch (Exception ex)
            {
                return BundleExecutionResult.Failed($"Execution error: {ex.Message}");
            }

            return await WaitForBundleReceiptAsync(bundle, transactionHash);
        }

        public async Task<string> SubmitAsync(Bundle bundle)
        {
            if (bundle.Entries.Length == 0)
            {
                throw new ArgumentException("Empty bundle");
            }

            var epService = GetEntryPointService(bundle.EntryPoint)
                ?? throw new ArgumentException($"Unsupported EntryPoint: {bundle.EntryPoint}");

            try
            {
                if (bundle.UsesAggregation)
                {
                    var aggregatedFunction = BuildHandleAggregatedOpsFunction(bundle);
                    var aggregatedGas = await epService.ContractHandler
                        .EstimateGasAsync(aggregatedFunction)
                        ?? throw new InvalidOperationException(
                            "handleAggregatedOps gas estimation returned no value (estimation disabled on the transaction manager?)");
                    aggregatedFunction.Gas = BigInteger.Max(aggregatedGas.Value, bundle.EstimatedGas);
                    return await epService.HandleAggregatedOpsRequestAsync(aggregatedFunction);
                }

                var handleOpsFunction = await BuildHandleOpsFunctionAsync(bundle);
                var gas = await EstimateHandleOpsGasAsync(epService, handleOpsFunction, bundle);
                handleOpsFunction.Gas = BigInteger.Max(gas, bundle.EstimatedGas);
                return await epService.HandleOpsRequestAsync(handleOpsFunction);
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                throw TranslateEntryPointRevert(ex);
            }
        }

        public async Task<BundleExecutionResult> WaitForBundleReceiptAsync(Bundle bundle, string transactionHash)
        {
            var receiptPolling = new TransactionReceiptPollingService(_web3.TransactionManager);
            using var timeout = new CancellationTokenSource(
                TimeSpan.FromSeconds(_config.BundleReceiptTimeoutSeconds));

            TransactionReceipt receipt;
            try
            {
                receipt = await receiptPolling.PollForReceiptAsync(transactionHash, timeout.Token);
            }
            catch (OperationCanceledException)
            {
                return new BundleExecutionResult
                {
                    Success = false,
                    TransactionHash = transactionHash,
                    ReceiptTimedOut = true,
                    Error = $"No receipt for bundle transaction {transactionHash} within " +
                            $"{_config.BundleReceiptTimeoutSeconds}s"
                };
            }

            if (receipt.Status?.Value != 1)
            {
                return new BundleExecutionResult
                {
                    Success = false,
                    TransactionHash = transactionHash,
                    Receipt = receipt,
                    GasUsed = receipt.GasUsed?.Value ?? 0,
                    Error = $"Transaction reverted: {transactionHash}"
                };
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

        private async Task<HandleOpsFunction> BuildHandleOpsFunctionAsync(Bundle bundle)
        {
            var ops = bundle.Entries
                .Select(e => ConvertToContractUserOp(e.UserOperation))
                .ToList();

            return new HandleOpsFunction
            {
                Ops = ops,
                Beneficiary = bundle.Beneficiary,
                AuthorisationList = await CollectEip7702AuthorisationsAsync(bundle)
            };
        }

        private async Task<BigInteger> EstimateHandleOpsGasAsync(
            EntryPointService epService, HandleOpsFunction handleOpsFunction, Bundle bundle)
        {
            if (handleOpsFunction.AuthorisationList == null)
            {
                var gas = await epService.ContractHandler.EstimateGasAsync(handleOpsFunction)
                    ?? throw new InvalidOperationException(
                        "handleOps gas estimation returned no value (estimation disabled on the transaction manager?)");
                return gas.Value;
            }

            try
            {
                var estimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(
                    BuildHandleOpsEstimateInput(epService, handleOpsFunction));
                return estimate.Value;
            }
            catch
            {
                return bundle.EstimatedGas
                    + (BigInteger)(handleOpsFunction.AuthorisationList.Count * Eip7702PerAuthGasCost)
                    + (BigInteger)(bundle.Entries.Length * HandleOpsEntryPointOverheadPerOp);
            }
        }

        private CallInput BuildHandleOpsEstimateInput(
            EntryPointService epService, HandleOpsFunction handleOpsFunction)
        {
            if (handleOpsFunction.AuthorisationList == null)
            {
                return handleOpsFunction.CreateCallInput(epService.ContractAddress);
            }

            var input = handleOpsFunction.CreateTransactionInput(epService.ContractAddress);
            input.From ??= _web3.TransactionManager?.Account?.Address;
            return input;
        }

        private async Task<List<Authorisation>?> CollectEip7702AuthorisationsAsync(Bundle bundle)
        {
            List<Authorisation>? authorisationList = null;
            var delegateBySigner = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

            foreach (var entry in bundle.Entries)
            {
                var auth = entry.Eip7702Auth;
                if (auth == null)
                {
                    continue;
                }

                string signer;
                try
                {
                    signer = auth.ToAuthorisation7702Signed().RecoverSignerAddress();
                }
                catch
                {
                    continue;
                }

                if (delegateBySigner.ContainsKey(signer))
                {
                    continue;
                }

                var senderCode = (await _web3.Eth.GetCode.SendRequestAsync(signer)).HexToByteArray();
                if (Eip7702DelegationUtils.IsDelegatedCode(senderCode) &&
                    Eip7702DelegationUtils.GetDelegateAddress(senderCode).IsTheSameAddress(auth.Address))
                {
                    continue;
                }

                delegateBySigner[signer] = auth.Address;
                authorisationList ??= new List<Authorisation>();
                authorisationList.Add(auth);
            }

            return authorisationList;
        }

        private HandleAggregatedOpsFunction BuildHandleAggregatedOpsFunction(Bundle bundle)
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

            return new HandleAggregatedOpsFunction
            {
                OpsPerAggregator = opsPerAggregator,
                Beneficiary = bundle.Beneficiary
            };
        }

        public static Exception TranslateEntryPointRevert(SmartContractCustomErrorRevertException ex)
        {
            if (ex.IsCustomErrorFor<FailedOpError>())
            {
                var error = ex.DecodeError<FailedOpError>();
                return new BundleFailedOpException(
                    (int)error.OpIndex,
                    error.Reason,
                    $"FailedOp: opIndex={error.OpIndex}, reason={error.Reason}");
            }

            if (ex.IsCustomErrorFor<FailedOpWithRevertError>())
            {
                var error = ex.DecodeError<FailedOpWithRevertError>();
                return new BundleFailedOpException(
                    (int)error.OpIndex,
                    error.Reason,
                    $"FailedOpWithRevert: opIndex={error.OpIndex}, reason={error.Reason}, inner={RevertReasonDecoder.Decode(error.Inner)}");
            }

            return new BundleSimulationRevertedException(
                ex.ExceptionEncodedData,
                $"handleOps simulation reverted without a decodable FailedOp, data: {ex.ExceptionEncodedData}",
                ex);
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
                var handleOpsFunction = await BuildHandleOpsFunctionAsync(bundle);
                var estimateInput = BuildHandleOpsEstimateInput(epService, handleOpsFunction);
                var estimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(estimateInput);

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

            var userOpEvents = receipt.Logs.DecodeAllEvents<UserOperationEventEventDTO>()
                .Where(e => e.Log?.Address?.Equals(bundle.EntryPoint, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();

            foreach (var entry in bundle.Entries)
            {
                var matchingEvent = userOpEvents.FirstOrDefault(e =>
                    e.Event.UserOpHash?.ToHex().Equals(entry.UserOpHash.Replace("0x", ""), StringComparison.OrdinalIgnoreCase) == true);

                if (matchingEvent != null)
                {
                    results.Add(new UserOpExecutionResult
                    {
                        UserOpHash = entry.UserOpHash,
                        EventFound = true,
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
                        EventFound = false,
                        Success = false,
                        Error = "No UserOperationEvent emitted for this operation in the bundle transaction"
                    });
                }
            }

            var revertEvents = receipt.Logs.DecodeAllEvents<UserOperationRevertReasonEventDTO>()
                .Where(e => e.Log?.Address?.Equals(bundle.EntryPoint, StringComparison.OrdinalIgnoreCase) == true)
                .ToList();
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

            return results.ToArray();
        }

    }
}
