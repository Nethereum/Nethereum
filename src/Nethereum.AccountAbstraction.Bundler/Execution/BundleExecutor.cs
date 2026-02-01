using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
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

        public BundleExecutor(IWeb3 web3, BundlerConfig config)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            foreach (var ep in config.SupportedEntryPoints)
            {
                _entryPoints[ep.ToLowerInvariant()] = new EntryPointService(web3, ep);
            }
        }

        public Task<Bundle> BuildBundleAsync(MempoolEntry[] entries)
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

            return Task.FromResult(bundle);
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
                var ops = bundle.Entries
                    .Select(e => ConvertToContractUserOp(e.UserOperation))
                    .ToList();

                var handleOpsFunction = new HandleOpsFunction
                {
                    Ops = ops,
                    Beneficiary = bundle.Beneficiary,
                    Gas = bundle.EstimatedGas
                };

                var receipt = await epService.HandleOpsRequestAndWaitForReceiptAsync(handleOpsFunction);

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
