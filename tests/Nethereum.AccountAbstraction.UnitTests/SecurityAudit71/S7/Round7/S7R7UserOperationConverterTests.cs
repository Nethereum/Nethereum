using Nethereum.AccountAbstraction.Structs;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.SecurityAudit71.S7.Round7
{
    public class S7R7UserOperationConverterTests
    {
        private static PackedUserOperation Packed(byte[] initCode) => new()
        {
            Sender = "0x1111111111111111111111111111111111111111",
            Nonce = 0,
            InitCode = initCode,
            CallData = System.Array.Empty<byte>(),
            AccountGasLimits = UserOperationBuilder.PackAccountGasLimits(100_000, 100_000),
            PreVerificationGas = 50_000,
            GasFees = UserOperationBuilder.PackAccountGasLimits(1_000_000_000, 2_000_000_000),
            PaymasterAndData = System.Array.Empty<byte>(),
            Signature = new byte[65]
        };

        [Fact]
        [Trait("Rule", "S7-R7-06")]
        [Trait("Erc", "EntryPoint Eip7702Support._isEip7702InitCode")]
        public void FromRpcFormat_WhenTheOpWasConvertedFromAnInitCodeThatIsJustTheTwoByteSentinel_KeepsTheInitCode()
        {
            var original = Packed("0x7702".HexToByteArray());

            var roundTripped = UserOperationConverter.FromRpcFormat(UserOperationConverter.ToRpcFormat(original));

            Assert.True(
                AAEIP7702Utils.IsEip7702UserOp(roundTripped.InitCode),
                "the EntryPoint reads a two byte 0x7702 initCode as an EIP-7702 marker; the conversion dropped it");
        }
    }
}
