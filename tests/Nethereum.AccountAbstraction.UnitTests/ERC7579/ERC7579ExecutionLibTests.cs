using Nethereum.AccountAbstraction.BaseAccount.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.Hex.HexConvertors.Extensions;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.ERC7579
{
    public class ERC7579ExecutionLibTests
    {
        [Fact]
        public void EncodeSingle_ShouldEncodeTargetValueAndData()
        {
            var target = "0x1234567890123456789012345678901234567890";
            var value = BigInteger.Parse("1000000000000000000");
            var callData = "0xabcdef".HexToByteArray();

            var encoded = ERC7579ExecutionLib.EncodeSingle(target, value, callData);

            Assert.Equal(20 + 32 + 3, encoded.Length);
        }

        [Fact]
        public void EncodeSingle_WithZeroValue_ShouldEncode()
        {
            var target = "0x1234567890123456789012345678901234567890";
            var value = BigInteger.Zero;
            var callData = "0x12345678".HexToByteArray();

            var encoded = ERC7579ExecutionLib.EncodeSingle(target, value, callData);

            Assert.Equal(20 + 32 + 4, encoded.Length);
        }

        [Fact]
        public void EncodeSingle_WithEmptyCallData_ShouldEncode()
        {
            var target = "0x1234567890123456789012345678901234567890";
            var value = BigInteger.Parse("100");
            var callData = new byte[0];

            var encoded = ERC7579ExecutionLib.EncodeSingle(target, value, callData);

            Assert.Equal(20 + 32, encoded.Length);
        }

        [Fact]
        public void DecodeSingle_ShouldRoundTrip()
        {
            var target = "0x1234567890123456789012345678901234567890";
            var value = BigInteger.Parse("1000000000000000000");
            var callData = "0xabcdef01".HexToByteArray();

            var encoded = ERC7579ExecutionLib.EncodeSingle(target, value, callData);
            var (decodedTarget, decodedValue, decodedCallData) = ERC7579ExecutionLib.DecodeSingle(encoded);

            Assert.Equal(target.ToLower(), decodedTarget.ToLower());
            Assert.Equal(value, decodedValue);
            Assert.Equal(callData, decodedCallData);
        }

        [Fact]
        public void EncodeBatch_WithMultipleCalls_ShouldEncode()
        {
            var calls = new Call[]
            {
                new Call
                {
                    Target = "0x1111111111111111111111111111111111111111",
                    Value = 100,
                    Data = "0x12345678".HexToByteArray()
                },
                new Call
                {
                    Target = "0x2222222222222222222222222222222222222222",
                    Value = 200,
                    Data = "0xabcdef".HexToByteArray()
                }
            };

            var encoded = ERC7579ExecutionLib.EncodeBatch(calls);

            Assert.NotNull(encoded);
            Assert.True(encoded.Length > 0);
        }

        [Fact]
        public void EncodeBatch_WithEmptyArray_ShouldReturnEmpty()
        {
            var calls = new Call[0];
            var encoded = ERC7579ExecutionLib.EncodeBatch(calls);

            Assert.Empty(encoded);
        }

        [Fact]
        public void EncodeBatch_WithNull_ShouldReturnEmpty()
        {
            var encoded = ERC7579ExecutionLib.EncodeBatch(null);
            Assert.Empty(encoded);
        }

        [Fact]
        public void ModuleTypes_ShouldHaveCorrectValues()
        {
            Assert.Equal(1, ERC7579ModuleTypes.TYPE_VALIDATOR);
            Assert.Equal(2, ERC7579ModuleTypes.TYPE_EXECUTOR);
            Assert.Equal(3, ERC7579ModuleTypes.TYPE_FALLBACK);
            Assert.Equal(4, ERC7579ModuleTypes.TYPE_HOOK);
        }
    }
}
