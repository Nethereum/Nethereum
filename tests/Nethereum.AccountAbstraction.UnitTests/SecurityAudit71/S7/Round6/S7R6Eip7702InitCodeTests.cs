using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.SecurityAudit71.S7.Round6
{
    public class S7R6Eip7702InitCodeTests
    {
        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 initCode")]
        public void IsEip7702UserOp_WhenTheInitCodeIsJustTheTwoByteSentinel_IsAMarker()
        {
            Assert.True(AAEIP7702Utils.IsEip7702UserOp("0x7702".HexToByteArray()));
        }

        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 initCode")]
        public void IsEip7702UserOp_WhenTheInitCodeIsTheSentinelAddressFollowedByFactoryData_IsAMarker()
        {
            var initCode = "0x7702000000000000000000000000000000000000".HexToByteArray()
                .Concat(new byte[] { 0x0c, 0x55, 0x69, 0x9c }).ToArray();

            Assert.True(AAEIP7702Utils.IsEip7702UserOp(initCode));
        }

        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 initCode")]
        public void IsEip7702UserOp_WhenTheInitCodeIsAFactoryWhoseAddressStartsWith0x7702_IsAFactory()
        {
            var initCode = "0x7702555555555555555555555555555555555555".HexToByteArray()
                .Concat(new byte[] { 0x01 }).ToArray();

            Assert.False(AAEIP7702Utils.IsEip7702UserOp(initCode));
        }

        [Fact]
        [Trait("Rule", "S7-R6-03")]
        [Trait("Erc", "ERC-4337 v0.8 EIP-7702 initCode")]
        public void IsEip7702UserOp_WhenTheInitCodeIsOneByteOrEmpty_IsNotAMarker()
        {
            Assert.False(AAEIP7702Utils.IsEip7702UserOp("0x77".HexToByteArray()));
            Assert.False(AAEIP7702Utils.IsEip7702UserOp(System.Array.Empty<byte>()));
        }
    }
}
