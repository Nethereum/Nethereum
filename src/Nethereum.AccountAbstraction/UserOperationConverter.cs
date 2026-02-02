using System;
using System.Linq;
using System.Numerics;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RLP;
using Nethereum.Util;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;

namespace Nethereum.AccountAbstraction
{
    public static class UserOperationConverter
    {
        public static RpcUserOperation ToRpcFormat(PackedUserOperation packed)
        {
            var rpc = new RpcUserOperation
            {
                Sender = packed.Sender,
                Nonce = new HexBigInteger(packed.Nonce),
                CallData = packed.CallData?.ToHex(true),
                Signature = packed.Signature?.ToHex(true),
                PreVerificationGas = new HexBigInteger(packed.PreVerificationGas)
            };

            UnpackInitCode(packed.InitCode, out var factory, out var factoryData);
            rpc.Factory = factory;
            rpc.FactoryData = factoryData?.ToHex(true);

            UnpackAccountGasLimits(packed.AccountGasLimits, out var verificationGasLimit, out var callGasLimit);
            rpc.VerificationGasLimit = new HexBigInteger(verificationGasLimit);
            rpc.CallGasLimit = new HexBigInteger(callGasLimit);

            UnpackGasFees(packed.GasFees, out var maxPriorityFeePerGas, out var maxFeePerGas);
            rpc.MaxPriorityFeePerGas = new HexBigInteger(maxPriorityFeePerGas);
            rpc.MaxFeePerGas = new HexBigInteger(maxFeePerGas);

            UnpackPaymasterAndData(packed.PaymasterAndData, out var paymaster, out var pmVerificationGas, out var pmPostOpGas, out var paymasterData);
            if (!string.IsNullOrEmpty(paymaster) && paymaster != AddressUtil.ZERO_ADDRESS)
            {
                rpc.Paymaster = paymaster;
                rpc.PaymasterVerificationGasLimit = new HexBigInteger(pmVerificationGas);
                rpc.PaymasterPostOpGasLimit = new HexBigInteger(pmPostOpGas);
                rpc.PaymasterData = paymasterData?.ToHex(true);
            }

            return rpc;
        }

        public static PackedUserOperation FromRpcFormat(RpcUserOperation rpc)
        {
            var initCode = PackInitCode(rpc.Factory, rpc.FactoryData?.HexToByteArray());
            var accountGasLimits = PackAccountGasLimits(
                rpc.VerificationGasLimit?.Value ?? 0,
                rpc.CallGasLimit?.Value ?? 0);
            var gasFees = PackGasFees(
                rpc.MaxPriorityFeePerGas?.Value ?? 0,
                rpc.MaxFeePerGas?.Value ?? 0);
            var paymasterAndData = PackPaymasterAndData(
                rpc.Paymaster,
                rpc.PaymasterVerificationGasLimit?.Value ?? 0,
                rpc.PaymasterPostOpGasLimit?.Value ?? 0,
                rpc.PaymasterData?.HexToByteArray());

            return new PackedUserOperation
            {
                Sender = rpc.Sender,
                Nonce = rpc.Nonce?.Value ?? 0,
                InitCode = initCode,
                CallData = rpc.CallData?.HexToByteArray() ?? Array.Empty<byte>(),
                AccountGasLimits = accountGasLimits,
                PreVerificationGas = rpc.PreVerificationGas?.Value ?? 0,
                GasFees = gasFees,
                PaymasterAndData = paymasterAndData,
                Signature = rpc.Signature?.HexToByteArray() ?? Array.Empty<byte>()
            };
        }

        public static RpcUserOperation ToRpcFormat(UserOperation userOp)
        {
            return new RpcUserOperation
            {
                Sender = userOp.Sender,
                Nonce = new HexBigInteger(userOp.Nonce ?? 0),
                Factory = GetFactoryFromInitCode(userOp.InitCode),
                FactoryData = GetFactoryDataFromInitCode(userOp.InitCode)?.ToHex(true),
                CallData = userOp.CallData?.ToHex(true),
                CallGasLimit = new HexBigInteger(userOp.CallGasLimit ?? 0),
                VerificationGasLimit = new HexBigInteger(userOp.VerificationGasLimit ?? 0),
                PreVerificationGas = new HexBigInteger(userOp.PreVerificationGas ?? 0),
                MaxFeePerGas = new HexBigInteger(userOp.MaxFeePerGas ?? 0),
                MaxPriorityFeePerGas = new HexBigInteger(userOp.MaxPriorityFeePerGas ?? 0),
                Paymaster = userOp.Paymaster,
                PaymasterVerificationGasLimit = new HexBigInteger(userOp.PaymasterVerificationGasLimit ?? 0),
                PaymasterPostOpGasLimit = new HexBigInteger(userOp.PaymasterPostOpGasLimit ?? 0),
                PaymasterData = userOp.PaymasterData?.ToHex(true),
                Signature = userOp.Signature?.ToHex(true)
            };
        }

        private static void UnpackInitCode(byte[] initCode, out string factory, out byte[] factoryData)
        {
            factory = null;
            factoryData = null;

            if (initCode == null || initCode.Length == 0)
                return;

            if (initCode.Length < 20)
                return;

            factory = initCode.Take(20).ToArray().ToHex(true);
            factoryData = initCode.Skip(20).ToArray();
        }

        private static byte[] PackInitCode(string factory, byte[] factoryData)
        {
            if (string.IsNullOrEmpty(factory) || factory == AddressUtil.ZERO_ADDRESS)
                return Array.Empty<byte>();

            var factoryBytes = factory.HexToByteArray();
            if (factoryData == null || factoryData.Length == 0)
                return factoryBytes;

            return ByteUtil.Merge(factoryBytes, factoryData);
        }

        private static string GetFactoryFromInitCode(byte[] initCode)
        {
            if (initCode == null || initCode.Length < 20)
                return null;

            return initCode.Take(20).ToArray().ToHex(true);
        }

        private static byte[] GetFactoryDataFromInitCode(byte[] initCode)
        {
            if (initCode == null || initCode.Length <= 20)
                return null;

            return initCode.Skip(20).ToArray();
        }

        private static void UnpackAccountGasLimits(byte[] accountGasLimits, out BigInteger verificationGasLimit, out BigInteger callGasLimit)
        {
            verificationGasLimit = 0;
            callGasLimit = 0;

            if (accountGasLimits == null || accountGasLimits.Length != 32)
                return;

            verificationGasLimit = accountGasLimits.Take(16).ToArray().ToBigIntegerFromRLPDecoded();
            callGasLimit = accountGasLimits.Skip(16).Take(16).ToArray().ToBigIntegerFromRLPDecoded();
        }

        private static byte[] PackAccountGasLimits(BigInteger verificationGasLimit, BigInteger callGasLimit)
        {
            var verificationBytes = ByteUtil.PadBytesLeft(verificationGasLimit.ToBytesForRLPEncoding(), 16);
            var callBytes = ByteUtil.PadBytesLeft(callGasLimit.ToBytesForRLPEncoding(), 16);
            return ByteUtil.Merge(verificationBytes, callBytes);
        }

        private static void UnpackGasFees(byte[] gasFees, out BigInteger maxPriorityFeePerGas, out BigInteger maxFeePerGas)
        {
            maxPriorityFeePerGas = 0;
            maxFeePerGas = 0;

            if (gasFees == null || gasFees.Length != 32)
                return;

            maxPriorityFeePerGas = gasFees.Take(16).ToArray().ToBigIntegerFromRLPDecoded();
            maxFeePerGas = gasFees.Skip(16).Take(16).ToArray().ToBigIntegerFromRLPDecoded();
        }

        private static byte[] PackGasFees(BigInteger maxPriorityFeePerGas, BigInteger maxFeePerGas)
        {
            var priorityBytes = ByteUtil.PadBytesLeft(maxPriorityFeePerGas.ToBytesForRLPEncoding(), 16);
            var maxBytes = ByteUtil.PadBytesLeft(maxFeePerGas.ToBytesForRLPEncoding(), 16);
            return ByteUtil.Merge(priorityBytes, maxBytes);
        }

        private static void UnpackPaymasterAndData(byte[] paymasterAndData, out string paymaster, out BigInteger verificationGas, out BigInteger postOpGas, out byte[] data)
        {
            paymaster = null;
            verificationGas = 0;
            postOpGas = 0;
            data = null;

            if (paymasterAndData == null || paymasterAndData.Length == 0)
                return;

            if (paymasterAndData.Length < 52)
                return;

            paymaster = paymasterAndData.Take(20).ToArray().ToHex(true);
            verificationGas = paymasterAndData.Skip(20).Take(16).ToArray().ToBigIntegerFromRLPDecoded();
            postOpGas = paymasterAndData.Skip(36).Take(16).ToArray().ToBigIntegerFromRLPDecoded();
            data = paymasterAndData.Skip(52).ToArray();
        }

        private static byte[] PackPaymasterAndData(string paymaster, BigInteger verificationGas, BigInteger postOpGas, byte[] data)
        {
            if (string.IsNullOrEmpty(paymaster) || paymaster == AddressUtil.ZERO_ADDRESS)
                return Array.Empty<byte>();

            var paymasterBytes = paymaster.HexToByteArray();
            var verificationBytes = ByteUtil.PadBytesLeft(verificationGas.ToBytesForRLPEncoding(), 16);
            var postOpBytes = ByteUtil.PadBytesLeft(postOpGas.ToBytesForRLPEncoding(), 16);

            if (data == null || data.Length == 0)
                return ByteUtil.Merge(paymasterBytes, verificationBytes, postOpBytes);

            return ByteUtil.Merge(paymasterBytes, verificationBytes, postOpBytes, data);
        }
    }
}
