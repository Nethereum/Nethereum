using System.Numerics;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    /// <summary>
    /// Validates UserOperations according to ERC-4337 rules.
    /// </summary>
    public class UserOpValidator : IUserOpValidator
    {
        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();

        public UserOpValidator(IWeb3 web3, BundlerConfig config)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));

            foreach (var ep in config.SupportedEntryPoints)
            {
                _entryPoints[ep.ToLowerInvariant()] = new EntryPointService(web3, ep);
            }
        }

        public async Task<UserOpValidationResult> ValidateAsync(PackedUserOperation userOp, string entryPoint)
        {
            var structureResult = await ValidateStructureAsync(userOp, entryPoint);
            if (!structureResult.IsValid)
            {
                return structureResult;
            }

            if (_config.SimulateValidation)
            {
                return await SimulateValidationAsync(userOp, entryPoint);
            }

            return structureResult;
        }

        public async Task<UserOpValidationResult> ValidateStructureAsync(PackedUserOperation userOp, string entryPoint)
        {
            if (!_entryPoints.ContainsKey(entryPoint.ToLowerInvariant()))
            {
                return UserOpValidationResult.Failure(
                    $"Unsupported EntryPoint: {entryPoint}",
                    UserOpValidationError.Unknown);
            }

            if (string.IsNullOrEmpty(userOp.Sender) || !userOp.Sender.IsValidEthereumAddressLength())
            {
                return UserOpValidationResult.Failure(
                    "Invalid sender address",
                    UserOpValidationError.InvalidSender);
            }

            if (userOp.Signature == null || userOp.Signature.Length == 0)
            {
                return UserOpValidationResult.Failure(
                    "Signature required",
                    UserOpValidationError.InvalidSignature);
            }

            if (userOp.AccountGasLimits == null || userOp.AccountGasLimits.Length != 32)
            {
                return UserOpValidationResult.Failure(
                    "Invalid accountGasLimits",
                    UserOpValidationError.GasValuesOverflow);
            }

            var (verificationGas, callGas) = UnpackAccountGasLimits(userOp.AccountGasLimits);

            if (verificationGas > _config.MaxVerificationGas)
            {
                return UserOpValidationResult.Failure(
                    $"Verification gas too high: {verificationGas} > {_config.MaxVerificationGas}",
                    UserOpValidationError.InsufficientVerificationGas);
            }

            if (userOp.GasFees == null || userOp.GasFees.Length != 32)
            {
                return UserOpValidationResult.Failure(
                    "Invalid gasFees",
                    UserOpValidationError.GasValuesOverflow);
            }

            var (maxPriorityFee, maxFee) = UnpackGasFees(userOp.GasFees);

            if (maxPriorityFee < _config.MinPriorityFeePerGas)
            {
                return UserOpValidationResult.Failure(
                    $"MaxPriorityFeePerGas too low: {maxPriorityFee} < {_config.MinPriorityFeePerGas}",
                    UserOpValidationError.MaxPriorityFeePerGasTooLow);
            }

            if (maxFee < maxPriorityFee)
            {
                return UserOpValidationResult.Failure(
                    "MaxFeePerGas must be >= MaxPriorityFeePerGas",
                    UserOpValidationError.MaxFeePerGasTooLow);
            }

            if (_config.StrictValidation)
            {
                if (userOp.InitCode != null && userOp.InitCode.Length > 0 && userOp.InitCode.Length < 20)
                {
                    return UserOpValidationResult.Failure(
                        "InitCode too short (must be empty or >= 20 bytes)",
                        UserOpValidationError.InitCodeFailed);
                }

                if (userOp.PaymasterAndData != null && userOp.PaymasterAndData.Length > 0 && userOp.PaymasterAndData.Length < 20)
                {
                    return UserOpValidationResult.Failure(
                        "PaymasterAndData too short (must be empty or >= 20 bytes)",
                        UserOpValidationError.PaymasterNotDeployed);
                }
            }

            var senderInitCodeResult = await ValidateSenderAndInitCodeAsync(userOp);
            if (!senderInitCodeResult.IsValid)
            {
                return senderInitCodeResult;
            }

            var epService = _entryPoints[entryPoint.ToLowerInvariant()];
            var nonceResult = await ValidateNonceAsync(userOp, epService);
            if (!nonceResult.IsValid)
            {
                return nonceResult;
            }

            var paymasterResult = await ValidatePaymasterAsync(userOp, epService);
            if (!paymasterResult.IsValid)
            {
                return paymasterResult;
            }

            var result = UserOpValidationResult.Success();
            result.VerificationGasLimit = verificationGas;
            result.CallGasLimit = callGas;
            result.PreVerificationGas = userOp.PreVerificationGas;

            return result;
        }

        public async Task<UserOpValidationResult> SimulateValidationAsync(PackedUserOperation userOp, string entryPoint)
        {
            var epService = GetEntryPointService(entryPoint);
            if (epService == null)
            {
                return UserOpValidationResult.Failure($"Unsupported EntryPoint: {entryPoint}");
            }

            try
            {
                var simulateResult = await SimulateHandleOpAsync(epService, userOp);

                if (!simulateResult.Success)
                {
                    return UserOpValidationResult.Failure(
                        simulateResult.Error ?? "Simulation failed",
                        UserOpValidationError.ExecutionReverted);
                }

                var result = UserOpValidationResult.Success();

                var (verificationGas, callGas) = UnpackAccountGasLimits(userOp.AccountGasLimits);
                result.VerificationGasLimit = verificationGas;
                result.CallGasLimit = callGas;
                result.PreVerificationGas = userOp.PreVerificationGas;

                return result;
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                var errorMessage = ParseEntryPointError(ex);
                return UserOpValidationResult.Failure(errorMessage, UserOpValidationError.ExecutionReverted);
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure($"Simulation error: {ex.Message}", UserOpValidationError.Unknown);
            }
        }

        public async Task<UserOpValidationResult> EstimateGasAsync(UserOperation userOp, string entryPoint)
        {
            var epService = GetEntryPointService(entryPoint);
            if (epService == null)
            {
                return UserOpValidationResult.Failure($"Unsupported EntryPoint: {entryPoint}");
            }

            try
            {
                var initializedOp = await epService.InitialiseUserOperationAsync(userOp);

                var result = UserOpValidationResult.Success();
                result.VerificationGasLimit = initializedOp.VerificationGasLimit ?? 0;
                result.CallGasLimit = initializedOp.CallGasLimit ?? 0;
                result.PreVerificationGas = initializedOp.PreVerificationGas ?? 0;

                return result;
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure($"Gas estimation error: {ex.Message}", UserOpValidationError.Unknown);
            }
        }

        private EntryPointService? GetEntryPointService(string entryPoint)
        {
            _entryPoints.TryGetValue(entryPoint.ToLowerInvariant(), out var service);
            return service;
        }

        private async Task<SimulationResult> SimulateHandleOpAsync(EntryPointService epService, PackedUserOperation userOp)
        {
            try
            {
                var callInput = new CallInput
                {
                    From = epService.ContractAddress,
                    To = userOp.Sender,
                    Data = userOp.CallData?.ToHex(true) ?? "0x",
                    Gas = new Nethereum.Hex.HexTypes.HexBigInteger(10_000_000)
                };

                await _web3.Eth.Transactions.Call.SendRequestAsync(callInput);

                return new SimulationResult { Success = true };
            }
            catch (Exception ex)
            {
                return new SimulationResult { Success = false, Error = ex.Message };
            }
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
                return $"FailedOpWithRevert: opIndex={error.OpIndex}, reason={error.Reason}, inner={error.Inner?.ToHex()}";
            }

            return ex.Message;
        }

        private static (BigInteger verificationGas, BigInteger callGas) UnpackAccountGasLimits(byte[] accountGasLimits)
        {
            if (accountGasLimits == null || accountGasLimits.Length < 32)
            {
                return (0, 0);
            }

            var verificationGas = new BigInteger(accountGasLimits.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            var callGas = new BigInteger(accountGasLimits.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            return (verificationGas, callGas);
        }

        private static (BigInteger maxPriorityFee, BigInteger maxFee) UnpackGasFees(byte[] gasFees)
        {
            if (gasFees == null || gasFees.Length < 32)
            {
                return (0, 0);
            }

            var maxPriorityFee = new BigInteger(gasFees.Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
            var maxFee = new BigInteger(gasFees.Skip(16).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());

            return (maxPriorityFee, maxFee);
        }

        private async Task<UserOpValidationResult> ValidateSenderAndInitCodeAsync(PackedUserOperation userOp)
        {
            var hasInitCode = userOp.InitCode != null && userOp.InitCode.Length >= 20;

            try
            {
                var senderCode = await _web3.Eth.GetCode.SendRequestAsync(userOp.Sender);
                var senderExists = !string.IsNullOrEmpty(senderCode) && senderCode != "0x" && senderCode != "0x0";

                if (hasInitCode)
                {
                    if (senderExists)
                    {
                        return UserOpValidationResult.Failure(
                            "AA10: sender already deployed - initCode must be empty",
                            UserOpValidationError.InitCodeFailed);
                    }

                    var factoryAddress = "0x" + userOp.InitCode.Take(20).ToArray().ToHex();
                    var factoryCode = await _web3.Eth.GetCode.SendRequestAsync(factoryAddress);
                    var factoryExists = !string.IsNullOrEmpty(factoryCode) && factoryCode != "0x" && factoryCode != "0x0";

                    if (!factoryExists)
                    {
                        return UserOpValidationResult.Failure(
                            "AA13: factory not deployed",
                            UserOpValidationError.InitCodeFailed);
                    }
                }
                else
                {
                    if (!senderExists)
                    {
                        return UserOpValidationResult.Failure(
                            "AA20: sender not deployed and no initCode",
                            UserOpValidationError.InvalidSender);
                    }
                }

                return UserOpValidationResult.Success();
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure(
                    $"Failed to validate sender/initCode: {ex.Message}",
                    UserOpValidationError.Unknown);
            }
        }

        private async Task<UserOpValidationResult> ValidateNonceAsync(PackedUserOperation userOp, EntryPointService epService)
        {
            try
            {
                var nonceKey = userOp.Nonce >> 64;
                var expectedNonce = await epService.GetNonceQueryAsync(userOp.Sender, nonceKey);

                if (userOp.Nonce != expectedNonce)
                {
                    return UserOpValidationResult.Failure(
                        $"AA25: invalid nonce - expected {expectedNonce}, got {userOp.Nonce}",
                        UserOpValidationError.InvalidNonce);
                }

                return UserOpValidationResult.Success();
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure(
                    $"Failed to validate nonce: {ex.Message}",
                    UserOpValidationError.Unknown);
            }
        }

        private async Task<UserOpValidationResult> ValidatePaymasterAsync(PackedUserOperation userOp, EntryPointService epService)
        {
            var hasPaymaster = userOp.PaymasterAndData != null && userOp.PaymasterAndData.Length >= 20;
            if (!hasPaymaster)
            {
                return UserOpValidationResult.Success();
            }

            try
            {
                var paymasterAddress = "0x" + userOp.PaymasterAndData.Take(20).ToArray().ToHex();

                if (paymasterAddress.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
                {
                    return UserOpValidationResult.Success();
                }

                var paymasterCode = await _web3.Eth.GetCode.SendRequestAsync(paymasterAddress);
                var paymasterExists = !string.IsNullOrEmpty(paymasterCode) && paymasterCode != "0x" && paymasterCode != "0x0";

                if (!paymasterExists)
                {
                    return UserOpValidationResult.Failure(
                        "AA30: paymaster not deployed",
                        UserOpValidationError.PaymasterNotDeployed);
                }

                var paymasterDeposit = await epService.BalanceOfQueryAsync(paymasterAddress);

                var (verificationGas, callGas) = UnpackAccountGasLimits(userOp.AccountGasLimits);
                var (_, maxFee) = UnpackGasFees(userOp.GasFees);

                BigInteger paymasterVerificationGas = 0;
                BigInteger paymasterPostOpGas = 0;
                if (userOp.PaymasterAndData.Length >= 52)
                {
                    paymasterVerificationGas = new BigInteger(
                        userOp.PaymasterAndData.Skip(20).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
                    paymasterPostOpGas = new BigInteger(
                        userOp.PaymasterAndData.Skip(36).Take(16).Reverse().Concat(new byte[] { 0 }).ToArray());
                }

                var totalGas = userOp.PreVerificationGas + verificationGas + callGas + paymasterVerificationGas + paymasterPostOpGas;
                var maxCost = totalGas * maxFee;

                if (paymasterDeposit < maxCost)
                {
                    return UserOpValidationResult.Failure(
                        $"AA31: paymaster deposit too low - required {maxCost}, available {paymasterDeposit}",
                        UserOpValidationError.PaymasterDepositTooLow);
                }

                return UserOpValidationResult.Success();
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure(
                    $"Failed to validate paymaster: {ex.Message}",
                    UserOpValidationError.Unknown);
            }
        }
    }
}
