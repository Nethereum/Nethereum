using System.Numerics;
using Nethereum.ABI;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.GasEstimation
{
    public interface IEvmGasEstimator
    {
        Task<EvmEstimationResult> EstimateGasAsync(string from, string to, byte[] data, BigInteger value, long gasLimit);
    }

    public class EvmEstimationResult
    {
        public bool Success { get; set; }
        public BigInteger GasUsed { get; set; }
        public string Error { get; set; }
    }

    public class UserOperationGasEstimator
    {
        private readonly IWeb3 _web3;
        private readonly string _entryPointAddress;
        private readonly IEvmGasEstimator _evmEstimator;
        private readonly string _bundlerAddress;

        public UserOperationGasEstimator(IWeb3 web3, string entryPointAddress, string bundlerAddress = null)
        {
            _web3 = web3;
            _entryPointAddress = entryPointAddress ?? throw new ArgumentNullException(nameof(entryPointAddress));
            _bundlerAddress = bundlerAddress ?? "0x0000000000000000000000000000000000000000";
        }

        public UserOperationGasEstimator(IEvmGasEstimator evmEstimator, string entryPointAddress, string bundlerAddress = null)
        {
            _evmEstimator = evmEstimator ?? throw new ArgumentNullException(nameof(evmEstimator));
            _entryPointAddress = entryPointAddress ?? throw new ArgumentNullException(nameof(entryPointAddress));
            _bundlerAddress = bundlerAddress ?? "0x0000000000000000000000000000000000000000";
        }

        public UserOperationGasEstimator(IWeb3 web3, IEvmGasEstimator evmEstimator, string entryPointAddress, string bundlerAddress = null)
        {
            _web3 = web3;
            _evmEstimator = evmEstimator;
            _entryPointAddress = entryPointAddress ?? throw new ArgumentNullException(nameof(entryPointAddress));
            _bundlerAddress = bundlerAddress ?? "0x0000000000000000000000000000000000000000";
        }

        public async Task<UserOperationGasEstimateResult> EstimateGasAsync(UserOperation userOp)
        {
            ValidateUserOperation(userOp);

            var result = new UserOperationGasEstimateResult();

            // PreVerificationGas - always calculated via formula
            result.PreVerificationGas = CalculatePreVerificationGas(userOp);

            // Try handleOps-based estimation first
            var handleOpsEstimation = await TryEstimateViaHandleOpsAsync(userOp);

            if (handleOpsEstimation.Success)
            {
                result.VerificationGasLimit = handleOpsEstimation.VerificationGasLimit;
            }
            else
            {
                // Fallback to legacy per-phase estimation
                result.VerificationGasLimit = await EstimateVerificationGasLegacyAsync(userOp);
            }

            // CallGasLimit - estimate separately
            result.CallGasLimit = await EstimateCallGasAsync(userOp);

            // Paymaster gas limits
            if (HasPaymaster(userOp))
            {
                result.PaymasterVerificationGasLimit = GasEstimationConstants.DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK;
                result.PaymasterPostOpGasLimit = GasEstimationConstants.DEFAULT_PAYMASTER_POST_OP_GAS_FALLBACK;
            }

            // Gas prices
            var (maxFeePerGas, maxPriorityFeePerGas) = await GetGasPricesAsync();
            result.MaxFeePerGas = maxFeePerGas;
            result.MaxPriorityFeePerGas = maxPriorityFeePerGas;

            return result;
        }

        private async Task<HandleOpsEstimationResult> TryEstimateViaHandleOpsAsync(UserOperation userOp)
        {
            var result = new HandleOpsEstimationResult { Success = false };

            try
            {
                // Create a copy with callGasLimit=0 so only verification runs
                var verificationOnlyOp = CreateVerificationOnlyUserOp(userOp);
                var packedOp = UserOperationBuilder.PackUserOperation(verificationOnlyOp);
                var handleOpsData = EncodeHandleOps(new[] { packedOp });

                BigInteger gasUsed;

                // Try Node RPC first if available
                if (_web3 != null)
                {
                    gasUsed = await EstimateHandleOpsViaNodeAsync(handleOpsData);
                }
                // Fall back to EVM simulation
                else if (_evmEstimator != null)
                {
                    gasUsed = await EstimateHandleOpsViaEvmAsync(handleOpsData);
                }
                else
                {
                    return result;
                }

                // Subtract fixed overhead and add buffer
                var verificationGas = gasUsed - GasEstimationConstants.HANDLE_OPS_FIXED_OVERHEAD;
                verificationGas = ApplyBuffer(verificationGas, GasEstimationConstants.VERIFICATION_GAS_BUFFER_PERCENT);
                verificationGas = BigInteger.Max(verificationGas, GasEstimationConstants.VERIFICATION_GAS_BUFFER);
                verificationGas = BigInteger.Min(verificationGas, GasEstimationConstants.MAX_VERIFICATION_GAS);

                result.Success = true;
                result.VerificationGasLimit = verificationGas;
            }
            catch
            {
                // Estimation failed, will use fallback
            }

            return result;
        }

        private async Task<BigInteger> EstimateHandleOpsViaNodeAsync(byte[] handleOpsData)
        {
            var callInput = new CallInput
            {
                From = _bundlerAddress,
                To = _entryPointAddress,
                Data = handleOpsData.ToHex(true),
                Gas = new HexBigInteger(GasEstimationConstants.MAX_SIMULATION_GAS)
            };

            var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(callInput);
            return gasEstimate.Value;
        }

        private async Task<BigInteger> EstimateHandleOpsViaEvmAsync(byte[] handleOpsData)
        {
            var evmResult = await _evmEstimator.EstimateGasAsync(
                _bundlerAddress,
                _entryPointAddress,
                handleOpsData,
                BigInteger.Zero,
                GasEstimationConstants.MAX_SIMULATION_GAS);

            if (!evmResult.Success)
            {
                throw new InvalidOperationException($"EVM estimation failed: {evmResult.Error}");
            }

            return evmResult.GasUsed;
        }

        private UserOperation CreateVerificationOnlyUserOp(UserOperation original)
        {
            return new UserOperation
            {
                Sender = original.Sender,
                Nonce = original.Nonce ?? 0,
                InitCode = original.InitCode ?? Array.Empty<byte>(),
                CallData = original.CallData ?? Array.Empty<byte>(),
                CallGasLimit = 0, // Key: set to 0 so only verification runs
                VerificationGasLimit = GasEstimationConstants.MAX_VERIFICATION_GAS,
                PreVerificationGas = GasEstimationConstants.PRE_VERIFICATION_OVERHEAD_GAS,
                MaxFeePerGas = original.MaxFeePerGas ?? 1_000_000_000,
                MaxPriorityFeePerGas = original.MaxPriorityFeePerGas ?? 1_000_000_000,
                Paymaster = original.Paymaster,
                PaymasterData = original.PaymasterData ?? Array.Empty<byte>(),
                PaymasterVerificationGasLimit = original.PaymasterVerificationGasLimit ?? GasEstimationConstants.DEFAULT_PAYMASTER_VERIFICATION_GAS_FALLBACK,
                PaymasterPostOpGasLimit = 0,
                Signature = new byte[GasEstimationConstants.SIGNATURE_SIZE]
            };
        }

        private byte[] EncodeHandleOps(PackedUserOperation[] ops)
        {
            var handleOpsFunction = new HandleOpsFunction
            {
                Ops = ops.ToList(),
                Beneficiary = _bundlerAddress
            };

            return handleOpsFunction.GetCallData();
        }

        public async Task<BigInteger> EstimateCallGasAsync(UserOperation userOp)
        {
            if (userOp.CallData == null || userOp.CallData.Length == 0)
            {
                return GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
            }

            try
            {
                BigInteger gasUsed;

                if (_web3 != null)
                {
                    var callInput = new CallInput
                    {
                        From = _entryPointAddress,
                        To = userOp.Sender,
                        Data = userOp.CallData.ToHex(true),
                        Gas = new HexBigInteger(GasEstimationConstants.MAX_SIMULATION_GAS)
                    };

                    var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(callInput);
                    gasUsed = gasEstimate.Value;
                }
                else if (_evmEstimator != null)
                {
                    var evmResult = await _evmEstimator.EstimateGasAsync(
                        _entryPointAddress,
                        userOp.Sender,
                        userOp.CallData,
                        BigInteger.Zero,
                        GasEstimationConstants.MAX_SIMULATION_GAS);

                    if (!evmResult.Success)
                    {
                        return GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
                    }

                    gasUsed = evmResult.GasUsed;
                }
                else
                {
                    return GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
                }

                gasUsed += GasEstimationConstants.INNER_GAS_OVERHEAD;
                gasUsed = ApplyBuffer(gasUsed, GasEstimationConstants.CALL_GAS_BUFFER_PERCENT);

                return BigInteger.Max(gasUsed, GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT);
            }
            catch
            {
                return GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
            }
        }

        private async Task<BigInteger> EstimateVerificationGasLegacyAsync(UserOperation userOp)
        {
            BigInteger totalVerificationGas = GasEstimationConstants.VERIFICATION_GAS_BUFFER;

            if (HasInitCode(userOp))
            {
                var deploymentGas = await EstimateAccountDeploymentGasAsync(userOp);
                totalVerificationGas += deploymentGas;
            }
            else
            {
                var accountValidationGas = await EstimateAccountValidationGasAsync(userOp);
                totalVerificationGas = BigInteger.Max(totalVerificationGas, accountValidationGas + 50000);
            }

            if (HasPaymaster(userOp))
            {
                totalVerificationGas += GasEstimationConstants.PAYMASTER_VALIDATION_GAS_BUFFER;
            }

            totalVerificationGas += GasEstimationConstants.FIXED_VERIFICATION_GAS_OVERHEAD;

            return BigInteger.Min(totalVerificationGas, GasEstimationConstants.MAX_VERIFICATION_GAS);
        }

        private async Task<BigInteger> EstimateAccountDeploymentGasAsync(UserOperation userOp)
        {
            if (!HasInitCode(userOp))
                return 0;

            var (factoryAddress, factoryData) = ParseInitCode(userOp.InitCode);

            try
            {
                if (_web3 != null)
                {
                    var callInput = new CallInput
                    {
                        From = _entryPointAddress,
                        To = factoryAddress,
                        Data = factoryData.ToHex(true),
                        Gas = new HexBigInteger(GasEstimationConstants.MAX_SIMULATION_GAS)
                    };

                    var gasEstimate = await _web3.Eth.Transactions.EstimateGas.SendRequestAsync(callInput);
                    return gasEstimate.Value + GasEstimationConstants.CREATE2_COST;
                }
                else if (_evmEstimator != null)
                {
                    var evmResult = await _evmEstimator.EstimateGasAsync(
                        _entryPointAddress,
                        factoryAddress,
                        factoryData,
                        BigInteger.Zero,
                        GasEstimationConstants.MAX_SIMULATION_GAS);

                    if (evmResult.Success)
                    {
                        return evmResult.GasUsed + GasEstimationConstants.CREATE2_COST;
                    }
                }
            }
            catch
            {
                // Fall through to default
            }

            return GasEstimationConstants.ACCOUNT_DEPLOYMENT_BASE_GAS + GasEstimationConstants.CREATE2_COST;
        }

        private async Task<BigInteger> EstimateAccountValidationGasAsync(UserOperation userOp)
        {
            if (string.IsNullOrEmpty(userOp.Sender))
                return GasEstimationConstants.DEFAULT_VERIFICATION_GAS_FALLBACK;

            try
            {
                if (_web3 != null)
                {
                    var codeSize = await _web3.Eth.GetCode.SendRequestAsync(userOp.Sender);
                    if (string.IsNullOrEmpty(codeSize) || codeSize == "0x" || codeSize == "0x0")
                    {
                        return GasEstimationConstants.ACCOUNT_DEPLOYMENT_BASE_GAS;
                    }
                }
            }
            catch
            {
                // Fall through to default
            }

            return GasEstimationConstants.VERIFICATION_GAS_BUFFER;
        }

        public BigInteger CalculatePreVerificationGas(UserOperation userOp)
        {
            var packedOp = PackUserOperationForGasEstimate(userOp);
            var encodedOp = new ABIEncode().GetABIParamsEncoded(packedOp);
            var calldataCost = CalculateCalldataCost(encodedOp);

            var fixedCost = GasEstimationConstants.BASE_TRANSACTION_GAS;

            var packedSize = encodedOp.Length;
            var wordCount = (packedSize + GasEstimationConstants.WORD_SIZE - 1) / GasEstimationConstants.WORD_SIZE;
            var perWordGas = wordCount * GasEstimationConstants.PER_USER_OP_WORD_GAS;

            var overhead = GasEstimationConstants.BASE_TRANSACTION_GAS;

            return fixedCost + calldataCost + perWordGas + overhead;
        }

        private async Task<(BigInteger maxFeePerGas, BigInteger maxPriorityFeePerGas)> GetGasPricesAsync()
        {
            try
            {
                if (_web3 != null)
                {
                    var block = await _web3.Eth.Blocks.GetBlockWithTransactionsByNumber
                        .SendRequestAsync(BlockParameter.CreateLatest());

                    var baseFee = block.BaseFeePerGas?.Value ?? 0;
                    var maxPriorityFee = 1_000_000_000;
                    var maxFee = baseFee + maxPriorityFee;

                    return (maxFee, maxPriorityFee);
                }
            }
            catch
            {
                // Fall through to default
            }

            return (1_000_000_000, 1_000_000_000);
        }

        private static BigInteger ApplyBuffer(BigInteger gas, int bufferPercent)
        {
            return (gas * (100 + bufferPercent)) / 100;
        }

        public static BigInteger CalculateCalldataCost(byte[] data)
        {
            if (data == null || data.Length == 0)
                return 0;

            BigInteger cost = 0;
            foreach (var b in data)
            {
                cost += b == 0
                    ? GasEstimationConstants.ZERO_BYTE_GAS_COST
                    : GasEstimationConstants.NON_ZERO_BYTE_GAS_COST;
            }
            return cost;
        }

        private static void ValidateUserOperation(UserOperation userOp)
        {
            if (userOp == null)
                throw new ArgumentNullException(nameof(userOp));

            if (string.IsNullOrEmpty(userOp.Sender))
                throw new ArgumentException("Sender address is required", nameof(userOp));

            if (userOp.Sender.IsTheSameAddress(AddressUtil.ZERO_ADDRESS))
            {
                if (!HasInitCode(userOp))
                {
                    throw new ArgumentException("Sender is zero address without initCode", nameof(userOp));
                }
            }
        }

        private static bool HasInitCode(UserOperation userOp)
        {
            return userOp.InitCode != null && userOp.InitCode.Length > 0;
        }

        private static bool HasPaymaster(UserOperation userOp)
        {
            return !string.IsNullOrEmpty(userOp.Paymaster) &&
                   !userOp.Paymaster.IsTheSameAddress(AddressUtil.ZERO_ADDRESS);
        }

        private static (string factoryAddress, byte[] factoryData) ParseInitCode(byte[] initCode)
        {
            if (initCode == null || initCode.Length < GasEstimationConstants.ADDRESS_SIZE)
                return (AddressUtil.ZERO_ADDRESS, Array.Empty<byte>());

            var factoryAddressBytes = new byte[GasEstimationConstants.ADDRESS_SIZE];
            Array.Copy(initCode, 0, factoryAddressBytes, 0, GasEstimationConstants.ADDRESS_SIZE);

            var factoryData = Array.Empty<byte>();
            if (initCode.Length > GasEstimationConstants.ADDRESS_SIZE)
            {
                factoryData = new byte[initCode.Length - GasEstimationConstants.ADDRESS_SIZE];
                Array.Copy(initCode, GasEstimationConstants.ADDRESS_SIZE, factoryData, 0, factoryData.Length);
            }

            return (factoryAddressBytes.ToHex(true), factoryData);
        }

        private static PackedUserOperation PackUserOperationForGasEstimate(UserOperation userOp)
        {
            var tempOp = new UserOperation
            {
                Sender = userOp.Sender ?? AddressUtil.ZERO_ADDRESS,
                Nonce = userOp.Nonce ?? 0,
                InitCode = userOp.InitCode ?? Array.Empty<byte>(),
                CallData = userOp.CallData ?? Array.Empty<byte>(),
                CallGasLimit = userOp.CallGasLimit ?? GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT,
                VerificationGasLimit = userOp.VerificationGasLimit ?? GasEstimationConstants.VERIFICATION_GAS_BUFFER,
                PreVerificationGas = userOp.PreVerificationGas ?? GasEstimationConstants.PRE_VERIFICATION_OVERHEAD_GAS,
                MaxFeePerGas = userOp.MaxFeePerGas ?? 1_000_000_000,
                MaxPriorityFeePerGas = userOp.MaxPriorityFeePerGas ?? 1_000_000_000,
                Paymaster = userOp.Paymaster,
                PaymasterData = userOp.PaymasterData ?? Array.Empty<byte>(),
                PaymasterVerificationGasLimit = userOp.PaymasterVerificationGasLimit ?? 0,
                PaymasterPostOpGasLimit = userOp.PaymasterPostOpGasLimit ?? 0,
                Signature = new byte[GasEstimationConstants.SIGNATURE_SIZE]
            };

            return UserOperationBuilder.PackUserOperation(tempOp);
        }
    }

    public class HandleOpsEstimationResult
    {
        public bool Success { get; set; }
        public BigInteger VerificationGasLimit { get; set; }
    }

    public class UserOperationGasEstimateResult
    {
        public BigInteger PreVerificationGas { get; set; }
        public BigInteger VerificationGasLimit { get; set; }
        public BigInteger CallGasLimit { get; set; }
        public BigInteger MaxFeePerGas { get; set; }
        public BigInteger MaxPriorityFeePerGas { get; set; }
        public BigInteger PaymasterVerificationGasLimit { get; set; }
        public BigInteger PaymasterPostOpGasLimit { get; set; }
    }
}
