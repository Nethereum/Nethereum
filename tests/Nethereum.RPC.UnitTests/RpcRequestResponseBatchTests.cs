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
using Microsoft.VisualStudio.TestPlatform.CommunicationUtilities;

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

    private class MockedRpcRequestHandler : IRpcRequestHandler<bool>
    {
        public string MethodName => throw new System.NotImplementedException();

        public IClient Client => throw new System.NotImplementedException();

        public bool DecodeResponse(RpcResponseMessage rpcResponseMessage)
        {
            throw new System.NotImplementedException();
        }
    };

    [Fact]
    public void RpcErrorNotOverritenWithFixOfExitEarly()
    {
        var item = new RpcRequestResponseBatchItem<MockedRpcRequestHandler, bool>(new MockedRpcRequestHandler(), new RpcRequest(1, "eth_call", new object[] { }));
        item.DecodeResponse(new RpcResponseMessage(1, CreateRpcError()));

        // should have error.
        Assert.True(item.HasError);
        
        // if its -1, it means the RpcRequestResponseBatchItem attempted to decode when it should have exited early now.
        Assert.NotEqual(-1, item.RpcError.Code); 
        Assert.NotEqual("Invalid format exception", item.RpcError.Message); 
        Assert.Equal(-100, item.RpcError.Code);
        Assert.Equal("something went wrong", item.RpcError.Message);
        Assert.Equal("0x0000000", item.RpcError.Data);
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
        JsonRpc.Client.RpcMessages.RpcError rpcError = CreateRpcError();

        var responseMessages = new List<RpcResponseMessage>() {
            new RpcResponseMessage(1, rpcError),
            new RpcResponseMessage(2, JToken.FromObject("0x01"))
        };

        return (batch, rpcError, responseMessages);
    }

    private static JsonRpc.Client.RpcMessages.RpcError CreateRpcError()
    {
        var jtoken = JToken.FromObject(new
        {
            code = -100,
            message = "something went wrong",
            data = "0x0000000"
        });

        var rpcError = jtoken.ToObject<JsonRpc.Client.RpcMessages.RpcError>();
        return rpcError;
    }
}
