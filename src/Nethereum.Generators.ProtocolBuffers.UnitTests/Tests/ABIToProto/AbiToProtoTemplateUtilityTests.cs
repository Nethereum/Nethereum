using Nethereum.Generators.Core;
using Nethereum.Generators.ProtocolBuffers.ABIToProto.CoreProto;
using Xunit;

namespace Nethereum.Generators.ProtocolBuffers.UnitTests.Tests.ABIToProto
{
    public class AbiToProtoTemplateUtilityTests
    {
        [Theory]
        [InlineData(ParameterDirection.Output, "Id",       "id")]
        [InlineData(ParameterDirection.Output, "AssetId",  "asset_id")]
        [InlineData(ParameterDirection.Output, "Asset1",   "asset_1")]
        [InlineData(ParameterDirection.Output, " Asset1",  "asset_1")]
        [InlineData(ParameterDirection.Output, "Asset1 ",  "asset_1")]
        [InlineData(ParameterDirection.Output, "Asset13",  "asset_13")]
        [InlineData(ParameterDirection.Output, "",         "return_value_1")]
        [InlineData(ParameterDirection.Output, "",         "return_value_2", 1)] //ensure counter is incremented
        [InlineData(ParameterDirection.Output, " ",        "return_value_1")]
        [InlineData(ParameterDirection.Output, null,       "return_value_1")]
        [InlineData(ParameterDirection.Input,  "",         "param_value_1")]
        [InlineData(ParameterDirection.Input,  " ",        "param_value_1")]
        [InlineData(ParameterDirection.Input,  null,       "param_value_1")]

        public void GenerateProtoFieldName(
            ParameterDirection parameterDirection, string abiParameterName, string expectedFieldName, int anonymousFieldCounter = 0)
        {
            Assert.Equal(
                expectedFieldName,
                ABIToProtoTemplateUtility.GenerateProtoFieldName(
                    abiParameterName, 1, parameterDirection, ref anonymousFieldCounter));
        }
    }
}
