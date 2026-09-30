using Nethereum.JsonRpc.Client.RpcMessages;
using Newtonsoft.Json;
using Xunit;

namespace Nethereum.RPC.UnitTests;

public class RpcErrorTolerantCodeTests
{
    private const string IntCodeResponse =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":-32602,\"message\":\"invalid params\"}}";

    private const string StringCodeResponse =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":\"INVALID_ARGUMENT\",\"message\":\"bad user op\"}}";

    private const string NumericStringNegativeResponse =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":\"-32602\",\"message\":\"invalid params\"}}";

    private const string NumericStringPositiveResponse =
        "{\"jsonrpc\":\"2.0\",\"id\":1,\"error\":{\"code\":\"32602\",\"message\":\"invalid params\"}}";

    [Fact]
    public void Newtonsoft_IntCode_DeserializesUnchanged()
    {
        var response = JsonConvert.DeserializeObject<RpcResponseMessage>(IntCodeResponse);

        Assert.True(response.HasError);
        Assert.Equal(-32602, response.Error.Code);
        Assert.Equal("invalid params", response.Error.Message);
    }

    [Fact]
    public void Newtonsoft_StringCode_DoesNotThrowAndSurfacesMessage()
    {
        var response = JsonConvert.DeserializeObject<RpcResponseMessage>(StringCodeResponse);

        Assert.True(response.HasError);
        Assert.Equal(0, response.Error.Code);
        Assert.Equal("bad user op", response.Error.Message);
    }

    [Fact]
    public void Newtonsoft_NumericStringCode_IsParsed()
    {
        Assert.Equal(-32602, JsonConvert.DeserializeObject<RpcResponseMessage>(NumericStringNegativeResponse).Error.Code);
        Assert.Equal(32602, JsonConvert.DeserializeObject<RpcResponseMessage>(NumericStringPositiveResponse).Error.Code);
    }

    [Fact]
    public void Newtonsoft_IntCode_SerializesAsNumber()
    {
        var response = JsonConvert.DeserializeObject<RpcResponseMessage>(IntCodeResponse);
        var json = JsonConvert.SerializeObject(response.Error);

        Assert.Contains("\"code\":-32602", json);
    }

    [Fact]
    public void SystemTextJson_IntCode_DeserializesUnchanged()
    {
        var response = System.Text.Json.JsonSerializer.Deserialize<RpcResponseMessage>(IntCodeResponse);

        Assert.True(response.HasError);
        Assert.Equal(-32602, response.Error.Code);
        Assert.Equal("invalid params", response.Error.Message);
    }

    [Fact]
    public void SystemTextJson_StringCode_DoesNotThrowAndSurfacesMessage()
    {
        var response = System.Text.Json.JsonSerializer.Deserialize<RpcResponseMessage>(StringCodeResponse);

        Assert.True(response.HasError);
        Assert.Equal(0, response.Error.Code);
        Assert.Equal("bad user op", response.Error.Message);
    }

    [Fact]
    public void SystemTextJson_NumericStringCode_IsParsed()
    {
        Assert.Equal(-32602, System.Text.Json.JsonSerializer.Deserialize<RpcResponseMessage>(NumericStringNegativeResponse).Error.Code);
        Assert.Equal(32602, System.Text.Json.JsonSerializer.Deserialize<RpcResponseMessage>(NumericStringPositiveResponse).Error.Code);
    }

    [Fact]
    public void SystemTextJson_IntCode_SerializesAsNumber()
    {
        var response = System.Text.Json.JsonSerializer.Deserialize<RpcResponseMessage>(IntCodeResponse);
        var json = System.Text.Json.JsonSerializer.Serialize(response.Error);

        Assert.Contains("\"code\":-32602", json);
    }
}
