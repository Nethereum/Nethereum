using Nethereum.JsonRpc.Client;
using Xunit;
using Moq;
using System.Numerics;
using Nethereum.JsonRpc.Client.RpcMessages;
using System.Collections.Generic;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Nethereum.Hex.HexTypes;
using System.Linq;

namespace Nethereum.RPC.UnitTests;

public class RpcRequestResponseBatchTests
{
    [Fact]
    public void DefaultSettingIsNotAllowPartialSuccess()
    {
        var batch = new RpcRequestResponseBatch();
        Assert.False(batch.AcceptPartiallySuccessful);
    }   

    [Fact]
    public void TestDefaultBatchMustThrowException()
    {
        var (batch, rpcError, responseMessages) = GetTestObjects();

        Assert.Throws<RpcResponseBatchException>(() =>
        {
            batch.UpdateBatchItemResponses(responseMessages);
        });
    }

    [Fact]
    public void TestDefaultBatchCanPartiallySucceedException()
    {
        var (batch, rpcError, responseMessages) = GetTestObjects();
        batch.AcceptPartiallySuccessful = true;

        batch.UpdateBatchItemResponses(responseMessages);

        Assert.True(batch.BatchItems.First(t => t.RpcRequestMessage.Id.ToString() == "1").HasError);
        Assert.False(batch.BatchItems.First(t => t.RpcRequestMessage.Id.ToString() == "2").HasError);
    }

    private static (RpcRequestResponseBatch batch, JsonRpc.Client.RpcMessages.RpcError rpcError, List<RpcResponseMessage> responseMessages) GetTestObjects()
    {
        var batch = new RpcRequestResponseBatch();
        batch.AcceptPartiallySuccessful = false;

        var failedBatchItem = new Mock<IRpcRequestResponseBatchItem>();
        failedBatchItem.Setup(t => t.RpcRequestMessage).Returns(new RpcRequestMessage(1, "eth_call", new object[] { }));
        failedBatchItem.Setup(t => t.HasError).Returns(true);

        var successBatchItem = new Mock<IRpcRequestResponseBatchItem>();
        successBatchItem.Setup(t => t.RpcRequestMessage).Returns(new RpcRequestMessage(2, "eth_call", new object[] { }));
        successBatchItem.Setup(t => t.HasError).Returns(false);

        batch.BatchItems.Add(successBatchItem.Object);
        batch.BatchItems.Add(failedBatchItem.Object);

        // only way i managed to create an RPC Error.
        var jtoken = JToken.FromObject(new
        {
            code = -1,
            message = "something went wrong",
            data = "0x0000000"
        });

        var rpcError = jtoken.ToObject<JsonRpc.Client.RpcMessages.RpcError>();

        var responseMessages = new List<RpcResponseMessage>() {
            new RpcResponseMessage(1, rpcError),
            new RpcResponseMessage(2, JToken.FromObject("0x01"))
        };

        return (batch, rpcError, responseMessages);
    }
}
