using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using RpcUserOperation = Nethereum.RPC.AccountAbstraction.DTOs.UserOperation;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Rpc
{
    public class Eip7702InitMarkerPackingTests
    {
        private const string MarkerAddressHex = "0x7702000000000000000000000000000000000000";

        [Fact]
        public void Sentinel_EncodesTo_INITCODE_EIP7702_MARKER()
        {
            Assert.Equal(
                AAEIP7702Utils.INITCODE_EIP7702_MARKER.ToHex(),
                "0x7702".HexToByteArray().ToHex());
        }

        [Fact]
        public void MarkerAddress_Is_Sentinel_PaddedTo20Bytes()
        {
            Assert.Equal(20, AAEIP7702Utils.INITCODE_EIP7702_MARKER_ADDRESS.Length);
            Assert.Equal(MarkerAddressHex, AAEIP7702Utils.INITCODE_EIP7702_MARKER_ADDRESS.ToHex(true));
        }

        [Fact]
        public void FromRpcFormat_FactorySentinelWithFactoryData_PadsMarkerToTwentyBytesThenData()
        {
            var factoryData = "0x0c55699c00000000000000000000000000000000000000000000000000000000000012fd";
            var rpc = new RpcUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = new HexBigInteger(0),
                Factory = "0x7702",
                FactoryData = factoryData
            };

            var packed = UserOperationConverter.FromRpcFormat(rpc);

            Assert.True(AAEIP7702Utils.IsEip7702UserOp(packed.InitCode));
            Assert.Equal(MarkerAddressHex + factoryData.Substring(2), packed.InitCode.ToHex(true));
            Assert.Equal(factoryData, ("0x" + packed.InitCode.Skip(20).ToArray().ToHex()));
        }

        [Fact]
        public void FromRpcFormat_FactorySentinelNoFactoryData_ProducesTwentyByteMarker()
        {
            var rpc = new RpcUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = new HexBigInteger(0),
                Factory = "0x7702"
            };

            var packed = UserOperationConverter.FromRpcFormat(rpc);

            Assert.True(AAEIP7702Utils.IsEip7702UserOp(packed.InitCode));
            Assert.Equal(20, packed.InitCode.Length);
            Assert.Equal(MarkerAddressHex, packed.InitCode.ToHex(true));
        }

        [Fact]
        public void BuildInitCode_MatchesFromRpcFormat_ForSentinelAndRealFactory()
        {
            var factoryData = "0xdeadbeef".HexToByteArray();

            var sentinel = AAEIP7702Utils.BuildInitCode("0x7702", factoryData);
            Assert.Equal(MarkerAddressHex + "deadbeef", sentinel.ToHex(true));

            var real = AAEIP7702Utils.BuildInitCode(
                "0xabababababababababababababababababababab", factoryData);
            Assert.Equal("0xabababababababababababababababababababab" + "deadbeef", real.ToHex(true));

            Assert.Empty(AAEIP7702Utils.BuildInitCode(null, factoryData));
            Assert.Empty(AAEIP7702Utils.BuildInitCode("0x", factoryData));
        }

        [Fact]
        public void FromRpcFormat_RealFactoryAddress_Unchanged()
        {
            var factory = "0xabababababababababababababababababababab";
            var factoryData = "0xdeadbeef";
            var rpc = new RpcUserOperation
            {
                Sender = "0x1111111111111111111111111111111111111111",
                Nonce = new HexBigInteger(0),
                Factory = factory,
                FactoryData = factoryData
            };

            var packed = UserOperationConverter.FromRpcFormat(rpc);

            Assert.False(AAEIP7702Utils.IsEip7702UserOp(packed.InitCode));
            Assert.Equal(factory + factoryData.Substring(2), packed.InitCode.ToHex(true));
        }
    }
}
