using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.EEST.ConformanceRunner
{
    public class RpcCompatSchemaShapeTests
    {
        [Fact]
        public void Given_txpool_status_shape_When_values_differ_Then_conforms()
        {
            var expected = JObject.Parse("{\"pending\":\"0x6\",\"queued\":\"0x0\"}");
            var served = JObject.Parse("{\"pending\":\"0x4\",\"queued\":\"0x0\"}");
            Assert.True(RpcCompatDriver.ShapeConforms(expected, served));
        }

        [Fact]
        public void Given_txpool_status_shape_When_a_field_has_wrong_type_Then_does_not_conform()
        {
            var expected = JObject.Parse("{\"pending\":\"0x6\",\"queued\":\"0x0\"}");
            var served = JObject.Parse("{\"pending\":{},\"queued\":\"0x0\"}");
            Assert.False(RpcCompatDriver.ShapeConforms(expected, served));
        }

        [Fact]
        public void Given_txpool_content_shape_When_served_pool_is_empty_Then_conforms()
        {
            var expected = JObject.Parse(
                "{\"pending\":{\"0xabc\":{\"0\":{\"nonce\":\"0x0\"}}},\"queued\":{}}");
            var served = JObject.Parse("{\"pending\":{},\"queued\":{}}");
            Assert.True(RpcCompatDriver.ShapeConforms(expected, served));
        }

        [Fact]
        public void Given_feeHistory_shape_When_all_fields_present_Then_conforms()
        {
            var expected = JObject.Parse(
                "{\"oldestBlock\":\"0x1b\",\"reward\":[[\"0x1\",\"0x1\"]],\"baseFeePerGas\":[\"0x1\",\"0x2\"]," +
                "\"gasUsedRatio\":[0.0016],\"baseFeePerBlobGas\":[\"0x0\",\"0x0\"],\"blobGasUsedRatio\":[0]}");
            var served = JObject.Parse(
                "{\"oldestBlock\":\"0x1b\",\"reward\":[[\"0x9\",\"0x9\"]],\"baseFeePerGas\":[\"0x1\",\"0x0\"]," +
                "\"gasUsedRatio\":[0.9],\"baseFeePerBlobGas\":[\"0x0\",\"0x0\"],\"blobGasUsedRatio\":[0.0]}");
            Assert.True(RpcCompatDriver.ShapeConforms(expected, served));
        }

        [Fact]
        public void Given_feeHistory_shape_When_a_blob_array_is_missing_Then_does_not_conform()
        {
            var expected = JObject.Parse(
                "{\"oldestBlock\":\"0x1b\",\"reward\":[[\"0x1\",\"0x1\"]],\"baseFeePerGas\":[\"0x1\",\"0x2\"]," +
                "\"gasUsedRatio\":[0.0016],\"baseFeePerBlobGas\":[\"0x0\",\"0x0\"],\"blobGasUsedRatio\":[0]}");
            var served = JObject.Parse(
                "{\"oldestBlock\":\"0x1b\",\"reward\":[[\"0x9\",\"0x9\"]],\"baseFeePerGas\":[\"0x1\",\"0x0\"]," +
                "\"gasUsedRatio\":[0.9]}");
            Assert.False(RpcCompatDriver.ShapeConforms(expected, served));
        }

        [Fact]
        public void Given_feeHistory_shape_When_an_array_element_type_is_wrong_Then_does_not_conform()
        {
            var expected = JObject.Parse("{\"gasUsedRatio\":[0.0016]}");
            var served = JObject.Parse("{\"gasUsedRatio\":[\"0x1\"]}");
            Assert.False(RpcCompatDriver.ShapeConforms(expected, served));
        }
    }
}
