using System.Numerics;
using Nethereum.ABI.FunctionEncoding;
using Nethereum.AccountAbstraction.Bundler.GasEstimation;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.AccountAbstraction.EntryPoint;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.EntryPointSimulations;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.AccountAbstraction.Validation;
using Nethereum.Contracts;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.Geth.RPC.GethEth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Web3;
using ValidationResult = Nethereum.AccountAbstraction.EntryPointSimulations.ValidationResult;
using PackedValidationData = Nethereum.AccountAbstraction.Validation.ValidationDataCodec;

namespace Nethereum.AccountAbstraction.Bundler.Validation
{
    public class UserOpValidator : IUserOpValidator
    {
        private const long SIMULATION_GAS = 10_000_000;

        private const ulong VALID_UNTIL_FUTURE_SECONDS = 30;

        private readonly IWeb3 _web3;
        private readonly BundlerConfig _config;
        private readonly Dictionary<string, EntryPointService> _entryPoints = new();
        private readonly IStakingInfoService _stakingInfoService;
        private readonly BundlerChainRules? _chainRules;
        private readonly IStateReader _nodeDataService;
        private readonly EthCall _ethCallWithStateOverride;
        private readonly IUserOpMempool? _mempool;

        public UserOpValidator(IWeb3 web3, BundlerConfig config)
            : this(web3, config, null, null, null)
        {
        }

        public UserOpValidator(
            IWeb3 web3,
            BundlerConfig config,
            IStateReader? nodeDataService,
            IStakingInfoService? stakingInfoService,
            IUserOpMempool? mempool = null)
        {
            _web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            _config = config ?? throw new ArgumentNullException(nameof(config));
            _mempool = mempool;

            foreach (var ep in config.SupportedEntryPoints)
            {
                _entryPoints[ep.ToLowerInvariant()] = new EntryPointService(web3, ep);
            }

            _nodeDataService = nodeDataService ?? Web3NodeDataServiceAdapter.CreateForLatest(web3);
            _stakingInfoService = stakingInfoService ?? new StakingInfoService(web3, config);
            _ethCallWithStateOverride = new EthCall(web3.Client);

            if (config.EnableERC7562Validation)
                _chainRules = new BundlerChainRules(web3, config);
        }

        public Task<UserOpValidationResult> ValidateAsync(PackedUserOperation userOp, string entryPoint)
            => ValidateAsync(userOp, entryPoint, new HashSet<string>(StringComparer.OrdinalIgnoreCase));

        public async Task<UserOpValidationResult> ValidateAsync(
            PackedUserOperation userOp, string entryPoint, ISet<string> accessedStorageAddresses,
            Authorisation eip7702Auth = null)
        {
            if (eip7702Auth != null)
            {
                var authPreCheck = await ValidateEip7702AuthAsync(userOp, eip7702Auth);
                if (!authPreCheck.IsValid)
                {
                    return authPreCheck;
                }
            }

            var structureResult = await ValidateStructureAsync(userOp, entryPoint, eip7702Auth);
            if (!structureResult.IsValid)
            {
                return structureResult;
            }

            if (_config.SimulateValidation)
            {
                var simResult = await SimulateValidationAsync(userOp, entryPoint, eip7702Auth);
                if (!simResult.IsValid)
                {
                    return simResult;
                }
                structureResult = simResult;
            }

            if (_config.EnableERC7562Validation && _chainRules != null)
            {
                var erc7562Simulation = new ERC7562SimulationService(
                    _nodeDataService, await _chainRules.ResolveAsync());

                var erc7562Result = await ValidateERC7562Async(userOp, entryPoint, accessedStorageAddresses, erc7562Simulation, eip7702Auth);
                if (!erc7562Result.IsValid)
                {
                    return erc7562Result;
                }
            }

            // ERC-7562 opcode/storage/staking rules) has run, so an op that both uses an
            // ERC-7562 rules before this "Currently not supporting aggregator" reject.
            if (!string.IsNullOrEmpty(structureResult.Aggregator))
            {
                return UserOpValidationResult.Failure(
                    "Currently not supporting aggregator",
                    UserOpValidationError.InvalidAggregator);
            }

            return structureResult;
        }

        private async Task<UserOpValidationResult> ValidateERC7562Async(
            PackedUserOperation userOp, string entryPoint, ISet<string> accessedStorageAddresses,
            ERC7562SimulationService erc7562Simulation,
            Authorisation eip7702Auth = null)
        {
            try
            {
                var senderInfo = await _stakingInfoService.GetSenderInfoAsync(userOp.Sender, entryPoint);
                var factoryInfo = await _stakingInfoService.GetFactoryInfoAsync(userOp.InitCode, entryPoint);
                var paymasterInfo = await _stakingInfoService.GetPaymasterInfoAsync(userOp.PaymasterAndData, entryPoint);

                var userOpDto = new PackedUserOperationDTO
                {
                    Sender = userOp.Sender,
                    Nonce = userOp.Nonce,
                    InitCode = userOp.InitCode ?? Array.Empty<byte>(),
                    CallData = userOp.CallData ?? Array.Empty<byte>(),
                    AccountGasLimits = userOp.AccountGasLimits ?? Array.Empty<byte>(),
                    PreVerificationGas = userOp.PreVerificationGas,
                    GasFees = userOp.GasFees ?? Array.Empty<byte>(),
                    PaymasterAndData = userOp.PaymasterAndData ?? Array.Empty<byte>(),
                    Signature = userOp.Signature ?? Array.Empty<byte>()
                };

                var chainId = _config.ChainId ?? await GetChainIdAsync();

                var result = await erc7562Simulation.ValidateUserOperationAsync(
                    userOpDto,
                    entryPoint,
                    senderInfo,
                    factoryInfo,
                    paymasterInfo,
                    aggregator: null,
                    blockNumber: -1,
                    timestamp: -1,
                    coinbase: null,
                    chainId: chainId,
                    eip7702Auth: eip7702Auth);

                foreach (var access in result.StorageAccesses)
                {
                    if (!string.IsNullOrEmpty(access.ContractAddress))
                    {
                        accessedStorageAddresses.Add(access.ContractAddress.ToLowerInvariant());
                    }
                }

                if (!result.IsValid)
                {
                    var violationMessages = result.Violations
                        .Select(v => $"{v.Rule}: {v.Message} ({(v.Entity.HasValue ? v.Entity.Value.ToErc7562EntityName() : "none")}@{v.Address})")
                        .ToList();

                    return UserOpValidationResult.Failure(
                        $"ERC-7562 validation failed: {string.Join("; ", violationMessages)}",
                        UserOpValidationError.InvalidOpcodeAccess);
                }

                return UserOpValidationResult.Success();
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure(
                    $"ERC-7562 validation error: {ex.Message}",
                    UserOpValidationError.InvalidOpcodeAccess);
            }
        }

        private async Task<BigInteger> GetChainIdAsync()
        {
            if (_config.ChainId.HasValue)
            {
                return _config.ChainId.Value;
            }

            var chainId = await _web3.Eth.ChainId.SendRequestAsync();
            return chainId.Value;
        }

        private async Task<UserOpValidationResult> ValidateEip7702AuthAsync(
            PackedUserOperation userOp, Authorisation eip7702Auth)
        {
            var chainId = _config.ChainId ?? await GetChainIdAsync();
            var authChainId = eip7702Auth.ChainId?.Value ?? BigInteger.Zero;

            if (authChainId != BigInteger.Zero && authChainId != chainId)
            {
                return UserOpValidationResult.Failure(
                    "Invalid chainId in authorization",
                    UserOpValidationError.InvalidAuthorisation);
            }

            string authority;
            try
            {
                authority = eip7702Auth.ToAuthorisation7702Signed().RecoverSignerAddress();
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure(
                    $"Authorization signer is not sender: {ex.Message}",
                    UserOpValidationError.InvalidAuthorisation);
            }

            if (!authority.IsTheSameAddress(userOp.Sender))
            {
                return UserOpValidationResult.Failure(
                    "Authorization signer is not sender",
                    UserOpValidationError.InvalidAuthorisation);
            }

            return UserOpValidationResult.Success();
        }

        public async Task<UserOpValidationResult> ValidateStructureAsync(
            PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null)
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

            if (userOp.AccountGasLimits == null || userOp.AccountGasLimits.Length != 32)
            {
                return UserOpValidationResult.Failure(
                    "Invalid accountGasLimits",
                    UserOpValidationError.GasValuesOverflow);
            }

            var (verificationGas, callGas) = userOp.UnpackAccountGasLimits();

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

            var (maxPriorityFee, maxFee) = userOp.UnpackGasFees();

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

            var minRequiredPreVerificationGas = Eip7623PreVerificationGasCalculator.CalculateMinRequired(
                userOp, Eip7623PreVerificationGasCalculator.MaxVerificationGasUsed);
            if (userOp.PreVerificationGas < minRequiredPreVerificationGas)
            {
                return UserOpValidationResult.Failure(
                    $"preVerificationGas too low: expected at least {minRequiredPreVerificationGas}, provided {userOp.PreVerificationGas}",
                    UserOpValidationError.GasValuesOverflow);
            }

            if (_config.StrictValidation)
            {
                // The EIP-7702 init-marker (0x7702 sentinel) is legitimately 2 bytes and is not a
                // factory address, so it is exempt from the "initCode must be empty or >= 20 bytes"
                if (!IsEip7702InitMarker(userOp, eip7702Auth)
                    && userOp.InitCode != null && userOp.InitCode.Length > 0 && userOp.InitCode.Length < 20)
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

            var senderInitCodeResult = await ValidateSenderAndInitCodeAsync(userOp, eip7702Auth);
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

        public async Task<UserOpValidationResult> SimulateValidationAsync(
            PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null)
        {
            var epService = GetEntryPointService(entryPoint);
            if (epService == null)
            {
                return UserOpValidationResult.Failure($"Unsupported EntryPoint: {entryPoint}");
            }

            try
            {
                var validation = await RunSimulateValidationAsync(userOp, entryPoint, eip7702Auth);
                return BuildSimulationResult(userOp, validation);
            }
            catch (SmartContractCustomErrorRevertException ex)
            {
                var errorMessage = ParseEntryPointError(ex);
                return UserOpValidationResult.Failure(errorMessage, ClassifyFailedOp(errorMessage));
            }
            catch (Exception ex)
            {
                return UserOpValidationResult.Failure($"Simulation error: {ex.Message}", UserOpValidationError.Unknown);
            }
        }

        private async Task<ValidationResult> RunSimulateValidationAsync(
            PackedUserOperation userOp, string entryPoint, Authorisation eip7702Auth = null)
        {
            var function = new SimulateValidationFunction { UserOp = userOp };
            var callInput = function.CreateTransactionInput(entryPoint);
            callInput.Gas = new Nethereum.Hex.HexTypes.HexBigInteger(SIMULATION_GAS);

            var stateOverride = new Dictionary<string, StateChange>
            {
                [entryPoint] = new StateChange { Code = EntryPointSimulationsRuntimeBytecode.V09 }
            };

            if (eip7702Auth != null)
            {
                callInput.AuthorisationList = new List<Authorisation> { eip7702Auth };
            }

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

        private UserOpValidationResult BuildSimulationResult(PackedUserOperation userOp, ValidationResult validation)
        {
            var returnInfo = validation.ReturnInfo;

            var (accountSigFailed, _, _, accountAggregator) =
                PackedValidationData.Parse(returnInfo.AccountValidationData);
            var (paymasterSigFailed, _, _, _) =
                PackedValidationData.Parse(returnInfo.PaymasterValidationData);

            if (accountSigFailed)
            {
                return UserOpValidationResult.Failure(
                    "AA24 signature error",
                    UserOpValidationError.InvalidSignature);
            }

            if (paymasterSigFailed)
            {
                return UserOpValidationResult.Failure(
                    "AA34 signature error",
                    UserOpValidationError.SignatureValidationFailed);
            }

            var merged = PackedValidationData.Merge(
                returnInfo.AccountValidationData, returnInfo.PaymasterValidationData);
            var (_, validUntil, validAfter, _) = PackedValidationData.Parse(merged);

            var now = (ulong)DateTimeOffset.UtcNow.ToUnixTimeSeconds();

            if (validAfter > now)
            {
                return UserOpValidationResult.Failure(
                    $"time-range in the future: validAfter={validAfter}, now={now}",
                    UserOpValidationError.NotYetValid);
            }

            if (validUntil != 0 && validUntil < now)
            {
                return UserOpValidationResult.Failure(
                    "already expired",
                    UserOpValidationError.ExpiredSignature);
            }

            if (validUntil != 0 && validUntil <= now + VALID_UNTIL_FUTURE_SECONDS)
            {
                return UserOpValidationResult.Failure(
                    "expires too soon",
                    UserOpValidationError.ExpiredSignature);
            }

            var result = UserOpValidationResult.Success();
            result.ValidationData = returnInfo.AccountValidationData;
            result.PaymasterValidationData = returnInfo.PaymasterValidationData;
            result.ValidAfter = validAfter;
            result.ValidUntil = validUntil;

            result.Aggregator = !string.IsNullOrEmpty(accountAggregator)
                ? accountAggregator
                : ExtractAggregator(validation);

            var (verificationGas, callGas) = userOp.UnpackAccountGasLimits();
            result.VerificationGasLimit = verificationGas;
            result.CallGasLimit = callGas;
            result.PreVerificationGas = userOp.PreVerificationGas;

            return result;
        }

        private static string? ExtractAggregator(ValidationResult validation)
        {
            var aggregator = validation.AggregatorInfo?.Aggregator;
            if (string.IsNullOrEmpty(aggregator) || aggregator.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
            {
                return null;
            }
            return aggregator;
        }

        private static UserOpValidationError ClassifyFailedOp(string errorMessage)
        {
            if (errorMessage.Contains("AA24")) return UserOpValidationError.InvalidSignature;
            if (errorMessage.Contains("AA34")) return UserOpValidationError.SignatureValidationFailed;
            if (errorMessage.Contains("AA22")) return UserOpValidationError.ExpiredSignature;
            if (errorMessage.Contains("AA32")) return UserOpValidationError.ExpiredSignature;
            return UserOpValidationError.ExecutionReverted;
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
                var initializedOp = await epService.InitialiseUserOperationAsync(userOp, userOp.Eip7702Auth?.Address);

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
                return $"FailedOpWithRevert: opIndex={error.OpIndex}, reason={error.Reason}, inner={RevertReasonDecoder.Decode(error.Inner)}";
            }

            return ex.Message;
        }

        private static bool IsEip7702InitMarker(PackedUserOperation userOp, Authorisation eip7702Auth)
            => eip7702Auth != null
               && userOp.InitCode != null
               && Nethereum.AccountAbstraction.AAEIP7702Utils.IsEip7702UserOp(userOp.InitCode);

        private async Task<UserOpValidationResult> ValidateSenderAndInitCodeAsync(
            PackedUserOperation userOp, Authorisation eip7702Auth = null)
        {
            if (IsEip7702InitMarker(userOp, eip7702Auth))
            {
                return UserOpValidationResult.Success();
            }

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
                        if (eip7702Auth != null)
                        {
                            return UserOpValidationResult.Success();
                        }

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
                var onChainNonce = await epService.GetNonceQueryAsync(userOp.Sender, nonceKey);

                var pendingSameKey = await GetPendingSameKeyEntriesAsync(userOp.Sender, epService.ContractAddress, nonceKey);

                if (pendingSameKey.Any(e => e.UserOperation.Nonce == userOp.Nonce))
                {
                    return UserOpValidationResult.Success();
                }

                var occupiedNonces = new HashSet<BigInteger>(pendingSameKey.Select(e => e.UserOperation.Nonce));
                var expectedNonce = onChainNonce;
                while (occupiedNonces.Contains(expectedNonce))
                {
                    expectedNonce += 1;
                }

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

        private async Task<MempoolEntry[]> GetPendingSameKeyEntriesAsync(string sender, string entryPoint, BigInteger nonceKey)
        {
            if (_mempool == null || string.IsNullOrEmpty(sender))
            {
                return Array.Empty<MempoolEntry>();
            }

            var bySender = await _mempool.GetBySenderAsync(sender);
            return bySender
                .Where(e => (e.State == MempoolEntryState.Pending || e.State == MempoolEntryState.Submitted)
                            && e.EntryPoint.IsTheSameAddress(entryPoint)
                            && (e.UserOperation.Nonce >> 64) == nonceKey)
                .ToArray();
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

                var (_, maxFee) = userOp.UnpackGasFees();
                var maxCost = userOp.GetTotalGas() * maxFee;

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
