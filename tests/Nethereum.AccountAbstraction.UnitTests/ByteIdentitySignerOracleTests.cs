using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.Tests
{
    public class ByteIdentitySignerOracleTests
    {
        private const string EntryPointAddress = "0x0000000071727De22E5E9d8BAf0edAc6f37da032";
        private static readonly BigInteger ChainId = 31337;
        private static readonly EthECKey Signer =
            new EthECKey("0xb5b1870957d373ef0eeffecc6e4812c0fd08f554b37b233526acc331bf1544f7");

        public static IEnumerable<object[]> FixedUserOperations()
        {
            yield return new object[] { "EmptyCallData", BuildEmptyCallDataOp() };
            yield return new object[] { "WithCallData", BuildWithCallDataOp() };
            yield return new object[] { "WithInitCode", BuildWithInitCodeOp() };
            yield return new object[] { "WithPaymasterAndData", BuildWithPaymasterAndDataOp() };
            yield return new object[] { "VaryingNonceAndGas", BuildVaryingNonceAndGasOp() };
        }

        private static UserOperation BuildEmptyCallDataOp() => new UserOperation
        {
            Sender = "0x000000000000000000000000000000000000dEaD",
            Nonce = 0,
            CallData = Array.Empty<byte>(),
            InitCode = Array.Empty<byte>(),
            CallGasLimit = 100000,
            VerificationGasLimit = 150000,
            PreVerificationGas = 21000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000
        };

        private static UserOperation BuildWithCallDataOp() => new UserOperation
        {
            Sender = "0x000000000000000000000000000000000000dEaD",
            Nonce = 1,
            CallData = "0xa9059cbb0000000000000000000000000000000000000000000000000000000000000dead0000000000000000000000000000000000000000000000000de0b6b3a7640000".HexToByteArray(),
            InitCode = Array.Empty<byte>(),
            CallGasLimit = 100000,
            VerificationGasLimit = 150000,
            PreVerificationGas = 21000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000
        };

        private static UserOperation BuildWithInitCodeOp() => new UserOperation
        {
            Sender = "0x000000000000000000000000000000000000dEaD",
            Nonce = 0,
            CallData = Array.Empty<byte>(),
            InitCode = "0x1234567890abcdef1234567890abcdef12345678deadbeef".HexToByteArray(),
            CallGasLimit = 100000,
            VerificationGasLimit = 300000,
            PreVerificationGas = 21000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000
        };

        private static UserOperation BuildWithPaymasterAndDataOp() => new UserOperation
        {
            Sender = "0x000000000000000000000000000000000000dEaD",
            Nonce = 0,
            CallData = Array.Empty<byte>(),
            InitCode = Array.Empty<byte>(),
            CallGasLimit = 100000,
            VerificationGasLimit = 150000,
            PreVerificationGas = 21000,
            MaxFeePerGas = 2000000000,
            MaxPriorityFeePerGas = 1000000000,
            Paymaster = "0x00000000000000000000000000000000000000aa",
            PaymasterData = "0xfeedface".HexToByteArray(),
            PaymasterVerificationGasLimit = 300000,
            PaymasterPostOpGasLimit = 50000
        };

        private static UserOperation BuildVaryingNonceAndGasOp() => new UserOperation
        {
            Sender = "0x000000000000000000000000000000000000dEaD",
            Nonce = 5,
            CallData = "0xdeadbeef".HexToByteArray(),
            InitCode = Array.Empty<byte>(),
            CallGasLimit = 500000,
            VerificationGasLimit = 750000,
            PreVerificationGas = 60000,
            MaxFeePerGas = 5000000000,
            MaxPriorityFeePerGas = 3000000000
        };

        [Theory]
        [MemberData(nameof(FixedUserOperations))]
        public void PackAndSignEIP712UserOperation_MatchesManualHashThenSign(string caseName, UserOperation userOperation)
        {
            var viaTypedDataSigner = UserOperationBuilder.PackAndSignEIP712UserOperation(
                userOperation, EntryPointAddress, ChainId, Signer);

            var digest = UserOperationBuilder.PackAndHashEIP712UserOperation(userOperation, EntryPointAddress, ChainId);
            var manualSignature = EthECDSASignature.CreateStringSignature(Signer.SignAndCalculateV(digest)).HexToByteArray();

            Assert.True(
                BytesEqual(viaTypedDataSigner.Signature, manualSignature),
                $"[{caseName}] SignTypedDataV4 path and manual hash-then-sign path diverged. " +
                $"typed={viaTypedDataSigner.Signature.ToHex()} manual={manualSignature.ToHex()}");
        }

        public static IEnumerable<object[]> GoldenSignatures()
        {
            yield return new object[]
            {
                "EmptyCallData",
                "0x41a0ded16787bc3f64a240712612fc6bd90a0eb8aee2ea5a7f00cdf2750674f24b2edd7c430ca6fb2f81f7a3ab9685189b2f7ce069ba30570831f5bfaa8ac21b1b"
            };
            yield return new object[]
            {
                "WithCallData",
                "0xf9f15e7690d93809842cdb41842888d141f90fc128c05127e9a88fa70a02c82f1afae31b58680aeb307d8173656df9e9edfc167fe1049020a74a090bdd1c47c01c"
            };
            yield return new object[]
            {
                "WithInitCode",
                "0x054ace44a2338570137c2d3516342e7e9dadb0b5c2401d96fdc6b9e6cfe89e64325ac2f1c1bd163de3b265633b453287c9e3a1ed106c6e39dc7a196a89a63aa91c"
            };
            yield return new object[]
            {
                "WithPaymasterAndData",
                "0x2fd8f3c0fe348e7180232af91dde6ab07432f3c37525735cbf2a465e6b4e32e4645625f3cfc3781aad11ca16352e5389939dadcd2c1f7365f71eaa9c507b89541c"
            };
            yield return new object[]
            {
                "VaryingNonceAndGas",
                "0xd3ee3812ceabb05db7369f4a3fdb327686046e250763af13e606c034f71075253956370fcea42d71b0e9ef5c8f7fcf95de1af20e367d7123e9f43c1a5afee58e1c"
            };
        }

        private static readonly Dictionary<string, UserOperation> OperationsByCaseName = new()
        {
            ["EmptyCallData"] = BuildEmptyCallDataOp(),
            ["WithCallData"] = BuildWithCallDataOp(),
            ["WithInitCode"] = BuildWithInitCodeOp(),
            ["WithPaymasterAndData"] = BuildWithPaymasterAndDataOp(),
            ["VaryingNonceAndGas"] = BuildVaryingNonceAndGasOp()
        };

        [Theory]
        [MemberData(nameof(GoldenSignatures))]
        public void PackAndSignEIP712UserOperation_MatchesFrozenGolden(string caseName, string expectedSignatureHex)
        {
            var userOperation = OperationsByCaseName[caseName];
            var signed = UserOperationBuilder.PackAndSignEIP712UserOperation(userOperation, EntryPointAddress, ChainId, Signer);

            Assert.Equal(expectedSignatureHex, signed.Signature.ToHex(prefix: true), ignoreCase: true);
        }

        private static bool BytesEqual(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            for (var i = 0; i < a.Length; i++)
                if (a[i] != b[i]) return false;
            return true;
        }
    }
}
