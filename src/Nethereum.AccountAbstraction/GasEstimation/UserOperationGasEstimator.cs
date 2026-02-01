using System.Numerics;
using Nethereum.ABI;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Util;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.GasEstimation
{
    public class UserOperationGasEstimator
    {
        private readonly IWeb3? _web3;
        private readonly string _entryPointAddress;

        public UserOperationGasEstimator(IWeb3? web3, string entryPointAddress)
        {
            _web3 = web3;
            _entryPointAddress = entryPointAddress ?? throw new ArgumentNullException(nameof(entryPointAddress));
        }

        private IWeb3 RequireWeb3()
        {
            return _web3 ?? throw new InvalidOperationException("Web3 instance is required for this operation");
        }

        public async Task<UserOperationGasEstimateResult> EstimateGasAsync(UserOperation userOp)
        {
            ValidateUserOperation(userOp);

            var result = new UserOperationGasEstimateResult();

            var preVerificationGas = CalculatePreVerificationGas(userOp);
            result.PreVerificationGas = preVerificationGas;

            var verificationGasLimit = await EstimateVerificationGasAsync(userOp);
            result.VerificationGasLimit = verificationGasLimit;

            var callGasLimit = await EstimateCallGasAsync(userOp);
            result.CallGasLimit = callGasLimit;

            if (HasPaymaster(userOp))
            {
                result.PaymasterVerificationGasLimit = GasEstimationConstants.VERIFICATION_GAS_BUFFER;
                result.PaymasterPostOpGasLimit = GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
            }

            var (maxFeePerGas, maxPriorityFeePerGas) = await GetGasPricesAsync();
            result.MaxFeePerGas = maxFeePerGas;
            result.MaxPriorityFeePerGas = maxPriorityFeePerGas;

            return result;
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

        public async Task<BigInteger> EstimateVerificationGasAsync(UserOperation userOp)
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
                totalVerificationGas = Math.Max((long)totalVerificationGas, (long)accountValidationGas + 50000);
            }

            if (HasPaymaster(userOp))
            {
                totalVerificationGas += GasEstimationConstants.PAYMASTER_VALIDATION_GAS_BUFFER;
            }

            totalVerificationGas += GasEstimationConstants.FIXED_VERIFICATION_GAS_OVERHEAD;

            return BigInteger.Min(totalVerificationGas, GasEstimationConstants.MAX_VERIFICATION_GAS);
        }

        public async Task<BigInteger> EstimateCallGasAsync(UserOperation userOp)
        {
            if (userOp.CallData == null || userOp.CallData.Length == 0)
            {
                return GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
            }

            try
            {
                var callInput = new CallInput
                {
                    From = userOp.Sender,
                    To = userOp.Sender,
                    Data = userOp.CallData.ToHex(true),
                    Gas = new HexBigInteger(10_000_000)
                };

                var gasEstimate = await RequireWeb3().Eth.Transactions.EstimateGas.SendRequestAsync(callInput);
                var estimatedGas = gasEstimate.Value;

                estimatedGas += GasEstimationConstants.INNER_GAS_OVERHEAD;
                estimatedGas = (estimatedGas * 120) / 100;

                return BigInteger.Max(estimatedGas, GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT);
            }
            catch
            {
                return GasEstimationConstants.DEFAULT_CALL_GAS_LIMIT;
            }
        }

        private async Task<BigInteger> EstimateAccountDeploymentGasAsync(UserOperation userOp)
        {
            if (!HasInitCode(userOp))
                return 0;

            var (factoryAddress, factoryData) = ParseInitCode(userOp.InitCode);

            try
            {
                var callInput = new CallInput
                {
                    From = _entryPointAddress,
                    To = factoryAddress,
                    Data = factoryData.ToHex(true),
                    Gas = new HexBigInteger(10_000_000)
                };

                var gasEstimate = await RequireWeb3().Eth.Transactions.EstimateGas.SendRequestAsync(callInput);
                return gasEstimate.Value + GasEstimationConstants.CREATE2_COST;
            }
            catch
            {
                return GasEstimationConstants.ACCOUNT_DEPLOYMENT_BASE_GAS + GasEstimationConstants.CREATE2_COST;
            }
        }

        private async Task<BigInteger> EstimateAccountValidationGasAsync(UserOperation userOp)
        {
            if (string.IsNullOrEmpty(userOp.Sender))
                return GasEstimationConstants.VERIFICATION_GAS_BUFFER;

            try
            {
                var codeSize = await RequireWeb3().Eth.GetCode.SendRequestAsync(userOp.Sender);
                if (string.IsNullOrEmpty(codeSize) || codeSize == "0x" || codeSize == "0x0")
                {
                    return GasEstimationConstants.ACCOUNT_DEPLOYMENT_BASE_GAS;
                }

                return GasEstimationConstants.VERIFICATION_GAS_BUFFER;
            }
            catch
            {
                return GasEstimationConstants.VERIFICATION_GAS_BUFFER;
            }
        }

        private async Task<(BigInteger maxFeePerGas, BigInteger maxPriorityFeePerGas)> GetGasPricesAsync()
        {
            try
            {
                var block = await RequireWeb3().Eth.Blocks.GetBlockWithTransactionsByNumber
                    .SendRequestAsync(BlockParameter.CreateLatest());

                var baseFee = block.BaseFeePerGas?.Value ?? 0;
                var maxPriorityFee = 1_000_000_000;
                var maxFee = baseFee + maxPriorityFee;

                return (maxFee, maxPriorityFee);
            }
            catch
            {
                return (1_000_000_000, 1_000_000_000);
            }
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

        private static Structs.PackedUserOperation PackUserOperationForGasEstimate(UserOperation userOp)
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
