using System.Numerics;
using Microsoft.Extensions.Logging;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPointSimulations;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.EVM.Execution;
using Nethereum.Geth.RPC.GethEth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.AccountAbstraction.DTOs;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;
using ExecutionResult = Nethereum.AccountAbstraction.EntryPointSimulations.ExecutionResult;
using ValidationResult = Nethereum.AccountAbstraction.EntryPointSimulations.ValidationResult;

namespace Nethereum.AccountAbstraction.Bundler.GasEstimation
{
    public class SimulationGasEstimator
    {
        private const long SIMULATION_GAS = 30_000_000;

        private static readonly BigInteger DEFAULT_SIMULATION_GAS_LIMIT = 10_000_000;
        private static readonly BigInteger DEFAULT_SIMULATION_PAYMASTER_GAS_LIMIT = 3_000_000;
        private static readonly BigInteger DEFAULT_SIMULATION_PRE_VERIFICATION_GAS = 21_000;

        private static readonly BigInteger MIN_VERIFICATION_GAS_LIMIT = 40_000;
        private static readonly BigInteger MIN_CALL_GAS_LIMIT = 21_000;

        private const int HEADROOM_NUMERATOR = 3;
        private const int HEADROOM_DENOMINATOR = 2;

        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly EthCall _ethCallWithStateOverride;
        private readonly ILogger? _logger;

        public SimulationGasEstimator(IWeb3 web3, BundlerConfig config, ILogger? logger = null)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _ethCallWithStateOverride = new EthCall(web3.Client);
            _logger = logger;
        }

        public async Task<UserOperationGasEstimate> EstimateAsync(UserOperation userOp, string entryPoint)
        {
            if (userOp == null) throw new ArgumentNullException(nameof(userOp));

            var simulationFee = BigInteger.One;

            var packedOp = await BuildSimulationPackedOpAsync(userOp, entryPoint, simulationFee);
            var stateOverride = new Dictionary<string, StateChange>
            {
                [entryPoint] = new StateChange { Code = EntryPointSimulationsRuntimeBytecode.V09 }
            };

            if (userOp.Eip7702Auth != null && !string.IsNullOrEmpty(userOp.Eip7702Auth.Address))
            {
                stateOverride[userOp.Sender] = new StateChange
                {
                    Code = Eip7702DelegationUtils.CreateDelegationCode(userOp.Eip7702Auth.Address).ToHex(true)
                };
            }

            ValidationResult validation;
            try
            {
                validation = await RunSimulateValidationAsync(packedOp, entryPoint, stateOverride);
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                throw ToBundlerRpcException(ex, BundlerErrorCodes.SimulateValidation);
            }

            var verificationGasLimit = BigInteger.Max(
                (validation.ReturnInfo.PreOpGas - packedOp.PreVerificationGas) * HEADROOM_NUMERATOR / HEADROOM_DENOMINATOR,
                MIN_VERIFICATION_GAS_LIMIT);

            ExecutionResult execution;
            try
            {
                execution = await RunSimulateHandleOpAsync(packedOp, userOp, entryPoint, stateOverride);
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                throw ToBundlerRpcException(ex, BundlerErrorCodes.SimulateValidation);
            }

            var rawExecutionComponent = execution.Paid / simulationFee - execution.PreOpGas;
            var actualExecutionGas = (rawExecutionComponent * 10 - DEFAULT_SIMULATION_GAS_LIMIT) / 9;

            var callGasLimit = BigInteger.Max(
                actualExecutionGas * HEADROOM_NUMERATOR / HEADROOM_DENOMINATOR,
                MIN_CALL_GAS_LIMIT);

            var (maxFeePerGas, maxPriorityFeePerGas) = await ResolveReturnedFeesAsync(userOp);
            var preVerificationGas = CalculatePreVerificationGas(
                packedOp, verificationGasLimit, callGasLimit, maxPriorityFeePerGas, maxFeePerGas);

            var result = new UserOperationGasEstimate
            {
                CallGasLimit = new HexBigInteger(callGasLimit),
                VerificationGasLimit = new HexBigInteger(verificationGasLimit),
                PreVerificationGas = new HexBigInteger(preVerificationGas),
                MaxFeePerGas = new HexBigInteger(maxFeePerGas),
                MaxPriorityFeePerGas = new HexBigInteger(maxPriorityFeePerGas)
            };

            if (HasPaymaster(userOp))
            {
                result.PaymasterVerificationGasLimit = new HexBigInteger(
                    IsUnsetOrZero(userOp.PaymasterVerificationGasLimit) ? DEFAULT_SIMULATION_PAYMASTER_GAS_LIMIT : userOp.PaymasterVerificationGasLimit!.Value);
                result.PaymasterPostOpGasLimit = new HexBigInteger(
                    IsUnsetOrZero(userOp.PaymasterPostOpGasLimit) ? DEFAULT_SIMULATION_PAYMASTER_GAS_LIMIT : userOp.PaymasterPostOpGasLimit!.Value);
            }

            return result;
        }

        private async Task<PackedUserOperation> BuildSimulationPackedOpAsync(UserOperation userOp, string entryPoint, BigInteger simulationFee)
        {
            var hasPaymaster = HasPaymaster(userOp);
            var nonce = userOp.Nonce ?? await new EntryPointService(_web3, entryPoint).GetNonceQueryAsync(userOp.Sender, 0);

            var simulationOp = new UserOperation
            {
                Sender = userOp.Sender,
                Nonce = nonce,
                InitCode = userOp.InitCode ?? Array.Empty<byte>(),
                CallData = userOp.CallData ?? Array.Empty<byte>(),
                CallGasLimit = DEFAULT_SIMULATION_GAS_LIMIT,
                VerificationGasLimit = IsUnsetOrZero(userOp.VerificationGasLimit) ? DEFAULT_SIMULATION_GAS_LIMIT : userOp.VerificationGasLimit!.Value,
                PreVerificationGas = userOp.PreVerificationGas ?? DEFAULT_SIMULATION_PRE_VERIFICATION_GAS,
                MaxFeePerGas = simulationFee,
                MaxPriorityFeePerGas = simulationFee,
                Paymaster = userOp.Paymaster,
                PaymasterData = userOp.PaymasterData ?? Array.Empty<byte>(),
                PaymasterVerificationGasLimit = hasPaymaster ? (IsUnsetOrZero(userOp.PaymasterVerificationGasLimit) ? DEFAULT_SIMULATION_PAYMASTER_GAS_LIMIT : userOp.PaymasterVerificationGasLimit!.Value) : 0,
                PaymasterPostOpGasLimit = hasPaymaster ? (IsUnsetOrZero(userOp.PaymasterPostOpGasLimit) ? DEFAULT_SIMULATION_PAYMASTER_GAS_LIMIT : userOp.PaymasterPostOpGasLimit!.Value) : 0,
                Signature = userOp.Signature ?? Array.Empty<byte>()
            };

            return UserOperationBuilder.PackUserOperation(simulationOp);
        }

        private async Task<ValidationResult> RunSimulateValidationAsync(
            PackedUserOperation packedOp, string entryPoint, Dictionary<string, StateChange> stateOverride)
        {
            var function = new SimulateValidationFunction { UserOp = packedOp };
            var callInput = function.CreateTransactionInput(entryPoint);
            callInput.Gas = new HexBigInteger(SIMULATION_GAS);

            try
            {
                var raw = await _ethCallWithStateOverride.SendRequestAsync(
                    callInput, BlockParameter.CreateLatest(), stateOverride);

                if (string.IsNullOrEmpty(raw) || raw == "0x")
                {
                    throw new InvalidOperationException(
                        "simulateValidation returned no data - the node may not support eth_call state overrides");
                }

                return new FunctionCallDecoder()
                    .DecodeFunctionOutput(new SimulateValidationOutputDTO(), raw)
                    .Result;
            }
            catch (RpcResponseException ex)
            {
                ContractRevertExceptionHandler.HandleContractRevertException(ex);
                throw;
            }
        }

        private async Task<ExecutionResult> RunSimulateHandleOpAsync(
            PackedUserOperation packedOp, UserOperation userOp, string entryPoint, Dictionary<string, StateChange> stateOverride)
        {
            var measurement = await ExecuteSimulateHandleOpAsync(
                packedOp, AddressUtil.ZERO_ADDRESS, Array.Empty<byte>(), entryPoint, stateOverride);

            if (userOp.CallData is { Length: > 0 })
            {
                var revertProbeOp = new PackedUserOperation
                {
                    Sender = packedOp.Sender,
                    Nonce = packedOp.Nonce,
                    InitCode = packedOp.InitCode,
                    CallData = Array.Empty<byte>(),
                    AccountGasLimits = packedOp.AccountGasLimits,
                    PreVerificationGas = packedOp.PreVerificationGas,
                    GasFees = packedOp.GasFees,
                    PaymasterAndData = packedOp.PaymasterAndData,
                    Signature = packedOp.Signature
                };

                ExecutionResult revertProbe;
                try
                {
                    revertProbe = await ExecuteSimulateHandleOpAsync(
                        revertProbeOp, userOp.Sender, userOp.CallData, entryPoint, stateOverride);
                }
                catch (SmartContractCustomErrorRevertException ex) when (IsEmptyCallDataProbeArtifact(ex))
                {
                    _logger?.LogWarning(
                        "Execution-revert detection skipped for UserOperation sender {Sender}, nonce {Nonce}: " +
                        "the empty-callData revert probe reverted with AA23, which only happens for a " +
                        "callData-inspecting validator (see RunSimulateHandleOpAsync's CAVEAT) - not evidence " +
                        "the real op would revert.",
                        userOp.Sender, packedOp.Nonce);
                    return measurement;
                }

                if (!revertProbe.TargetSuccess)
                {
                    var revertData = revertProbe.TargetResult is { Length: > 0 }
                        ? revertProbe.TargetResult.ToHex(true)
                        : null;

                    throw new BundlerRpcException(
                        BundlerErrorCodes.UserOperationReverted,
                        $"UserOperation reverted during execution: {RevertReasonDecoder.Decode(revertProbe.TargetResult)}",
                        revertData);
                }
            }

            return measurement;
        }

        private async Task<ExecutionResult> ExecuteSimulateHandleOpAsync(
            PackedUserOperation op, string target, byte[] targetCallData, string entryPoint, Dictionary<string, StateChange> stateOverride)
        {
            var function = new SimulateHandleOpFunction
            {
                Op = op,
                Target = target,
                TargetCallData = targetCallData
            };
            var callInput = function.CreateTransactionInput(entryPoint);
            callInput.Gas = new HexBigInteger(SIMULATION_GAS);

            try
            {
                var raw = await _ethCallWithStateOverride.SendRequestAsync(
                    callInput, BlockParameter.CreateLatest(), stateOverride);

                if (string.IsNullOrEmpty(raw) || raw == "0x")
                {
                    throw new InvalidOperationException(
                        "simulateHandleOp returned no data - the node may not support eth_call state overrides");
                }

                return new FunctionCallDecoder()
                    .DecodeFunctionOutput(new SimulateHandleOpOutputDTO(), raw)
                    .Result;
            }
            catch (RpcResponseException ex)
            {
                ContractRevertExceptionHandler.HandleContractRevertException(ex);
                throw;
            }
        }

        private static BigInteger CalculatePreVerificationGas(
            PackedUserOperation packedOp,
            BigInteger verificationGasLimit,
            BigInteger callGasLimit,
            BigInteger maxPriorityFeePerGas,
            BigInteger maxFeePerGas)
        {
            var pvgSizingOp = new PackedUserOperation
            {
                Sender = packedOp.Sender,
                Nonce = packedOp.Nonce,
                InitCode = packedOp.InitCode,
                CallData = packedOp.CallData,
                AccountGasLimits = Nethereum.AccountAbstraction.UserOperationBuilder.PackAccountGasLimits(
                    verificationGasLimit, callGasLimit),
                PreVerificationGas = packedOp.PreVerificationGas,
                GasFees = Nethereum.AccountAbstraction.UserOperationBuilder.PackAccountGasLimits(
                    maxPriorityFeePerGas, maxFeePerGas),
                PaymasterAndData = packedOp.PaymasterAndData,
                Signature = WorstCaseCalldataBytes(packedOp.Signature)
            };

            return Eip7623PreVerificationGasCalculator.CalculateMinRequired(pvgSizingOp, verificationGasUsed: 0);
        }

        private static byte[] WorstCaseCalldataBytes(byte[] source)
        {
            if (source == null || source.Length == 0) return Array.Empty<byte>();

            var worstCase = new byte[source.Length];
            for (var i = 0; i < worstCase.Length; i++) worstCase[i] = 0xff;
            return worstCase;
        }

        private async Task<(BigInteger MaxFeePerGas, BigInteger MaxPriorityFeePerGas)> ResolveReturnedFeesAsync(UserOperation userOp)
        {
            if (userOp.MaxFeePerGas.HasValue && userOp.MaxFeePerGas.Value > 0)
            {
                return (userOp.MaxFeePerGas.Value, userOp.MaxPriorityFeePerGas ?? userOp.MaxFeePerGas.Value);
            }

            var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber.SendRequestAsync(BlockParameter.CreateLatest());
            var maxPriorityFeePerGas = userOp.MaxPriorityFeePerGas ?? UserOperation.DEFAULT_MAX_PRIORITY_FEE_PER_GAS;
            var maxFeePerGas = (block.BaseFeePerGas?.Value ?? 0) + maxPriorityFeePerGas;
            return (maxFeePerGas, maxPriorityFeePerGas);
        }

        private static bool IsEmptyCallDataProbeArtifact(SmartContractCustomErrorRevertException ex) =>
            ex.IsCustomErrorFor<FailedOpWithRevertError>() &&
            ex.DecodeError<FailedOpWithRevertError>().Reason == "AA23 reverted";

        private static BundlerRpcException ToBundlerRpcException(SmartContractCustomErrorRevertException ex, int errorCode)
        {
            if (ex.IsCustomErrorFor<FailedOpError>())
            {
                var error = ex.DecodeError<FailedOpError>();
                return new BundlerRpcException(errorCode, error.Reason);
            }

            if (ex.IsCustomErrorFor<FailedOpWithRevertError>())
            {
                var error = ex.DecodeError<FailedOpWithRevertError>();
                var inner = error.Inner is { Length: > 0 } ? $", inner={RevertReasonDecoder.Decode(error.Inner)}" : "";
                return new BundlerRpcException(errorCode, $"{error.Reason}{inner}");
            }

            return new BundlerRpcException(errorCode, ex.Message);
        }

        private static bool IsUnsetOrZero(BigInteger? value) => value == null || value.Value == BigInteger.Zero;

        private static bool HasPaymaster(UserOperation userOp) =>
            !userOp.Paymaster.IsAnEmptyAddress() &&
            userOp.Paymaster.IsValidEthereumAddressLength() &&
            !userOp.Paymaster.IsTheSameAddress(AddressUtil.ZERO_ADDRESS);
    }
}
