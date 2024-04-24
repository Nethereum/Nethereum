using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Mud.EncodingDecoding;

namespace Nethereum.Mud.UnitTests
{
    public class ResourceTests
    {
        [Fact]
        public void ShouldEncodeResourceId()
        {
            var result = ResourceEncoder.Encode(Resource.RESOURCE_TABLE, "store", "Tables");
            Assert.Equal("0x746273746f72650000000000000000005461626c657300000000000000000000", result.ToHex(true));
            var resource = ResourceEncoder.Decode(result);
            Assert.True(resource.IsTable);
            Assert.Equal("store", resource.Namespace);
            Assert.Equal("Tables", resource.Name);

            result = ResourceEncoder.Encode(Resource.RESOURCE_NAMESPACE, "store");
            Assert.Equal("0x6e7373746f726500000000000000000000000000000000000000000000000000", result.ToHex(true));
            resource = ResourceEncoder.Decode(result);
            Assert.True(resource.IsNamespace);
            Assert.Equal("store", resource.Namespace);
            Assert.Equal(String.Empty, resource.Name);


            result = ResourceEncoder.Encode(Resource.RESOURCE_OFFCHAIN_TABLE, "world", "FunctionSignatur");
            Assert.Equal("0x6f74776f726c6400000000000000000046756e6374696f6e5369676e61747572", result.ToHex(true));
            resource = ResourceEncoder.Decode(result);
            Assert.True(resource.IsOffchainTable);
            Assert.Equal("world", resource.Namespace);
            Assert.Equal("FunctionSignatur", resource.Name);

            result = ResourceEncoder.Encode(Resource.RESOURCE_SYSTEM, "", "AccessManagement");
            Assert.Equal("0x737900000000000000000000000000004163636573734d616e6167656d656e74", result.ToHex(true));
            resource = ResourceEncoder.Decode(result);
            Assert.True(resource.IsSystem);
            Assert.Equal(String.Empty, resource.Namespace);
            Assert.Equal("AccessManagement", resource.Name);
            Assert.True(resource.IsRoot);
        }
    }
}