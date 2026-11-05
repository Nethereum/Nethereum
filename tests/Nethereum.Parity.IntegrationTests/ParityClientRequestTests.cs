using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Parity.RPC;
using Nethereum.Parity.RPC.PubSub;
using Nethereum.Parity.RPC.Trace;
using Nethereum.RPC.Eth.DTOs;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Nethereum.Parity.IntegrationTests
{
    public class ParityClientRequestTests
    {
        private const string TransactionHash = "0xc8459467a4f917ea9550e2d35a9a8faefcb9805de4be0cd29ce01471e9cedc29";

        private sealed class CannedResponseClient : ClientBase
        {
            private readonly Func<string, JToken> _result;

            public CannedResponseClient(Func<string, JToken> result)
            {
                _result = result;
            }

            public List<RpcRequestMessage> Sent { get; } = new List<RpcRequestMessage>();

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
            {
                Sent.Add(rpcRequestMessage);
                return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _result(rpcRequestMessage.Method)));
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests)
            {
                throw new NotSupportedException();
            }
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "parity-client", "Call parity_* methods through Web3Parity", Order = 1)]
        public async Task ShouldCallParityMethodsThroughTheServiceGroups()
        {
            var client = new CannedResponseClient(method =>
            {
                switch (method)
                {
                    case "parity_versionInfo":
                        return JObject.Parse("{\"hash\":\"0x2ae8b4ca278dd7b896090366615fef81cbbbc0e0\",\"track\":\"null\",\"version\":{\"major\":1,\"minor\":6,\"patch\":0}}");
                    case "parity_netPort":
                        return new JValue(30303);
                    case "parity_minGasPrice":
                        return new JValue("0x3b9aca00");
                    default:
                        throw new InvalidOperationException(method);
                }
            });
            var web3 = new Web3Parity(client);

            var versionInfo = await web3.Admin.VersionInfo.SendRequestAsync();
            var netPort = await web3.Network.NetPort.SendRequestAsync();
            var minGasPrice = await web3.BlockAuthoring.MinGasPrice.SendRequestAsync();

            Assert.Equal("null", (string)versionInfo["track"]);
            Assert.Equal(1, (int)versionInfo["version"]["major"]);
            Assert.Equal(30303, netPort);
            Assert.Equal(1000000000, (long)minGasPrice.Value);
            Assert.Equal(new[] { "parity_versionInfo", "parity_netPort", "parity_minGasPrice" },
                client.Sent.Select(r => r.Method).ToArray());
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "parity-client", "Trace a transaction and build a trace_call request", Order = 2)]
        public async Task ShouldTraceATransactionAndBuildATraceCallRequest()
        {
            var client = new CannedResponseClient(method => JArray.Parse("[{\"type\":\"call\"}]"));
            var web3 = new Web3Parity(client);

            var traces = await web3.Trace.TraceTransaction.SendRequestAsync(TransactionHash);

            var callInput = new CallInput { To = "0x12890d2cce102216644c59daE5baed380d84830c" };
            var request = web3.Trace.TraceCall.BuildRequest(
                callInput, new[] { TraceType.trace, TraceType.stateDiff }, BlockParameter.CreateLatest());

            Assert.Equal("call", (string)traces[0]["type"]);
            Assert.Equal("trace_transaction", client.Sent.Single().Method);
            Assert.Equal(TransactionHash, (string)((object[])client.Sent.Single().RawParameters)[0]);
            Assert.Equal("trace_call", request.Method);
            Assert.Same(callInput, request.RawParameters[0]);
            Assert.Equal(new[] { "trace", "stateDiff" }, (string[])request.RawParameters[1]);
        }

        [Fact]
        [NethereumDocExample(DocSection.ClientExtensions, "parity-client", "Wrap a request in parity_subscribe", Order = 3)]
        public void ShouldWrapARequestInAParitySubscribeRequest()
        {
            var original = new RpcRequest(1, "eth_getBalance", "0x12890d2cce102216644c59daE5baed380d84830c", "latest");

            var subscribe = new ParitySubscribeRequestBuilder().BuildRequest(original, "sub-1");
            var unsubscribe = new ParityUnsubscribeRequestBuilder().BuildRequest("abcd", "unsub-1");

            Assert.Equal("parity_subscribe", new ParitySubscribeRequestBuilder().MethodName);
            Assert.Equal("sub-1", subscribe.Id);
            Assert.Equal("eth_getBalance", subscribe.RawParameters[0]);
            Assert.Equal(original.RawParameters, (object[])subscribe.RawParameters[1]);
            Assert.Equal("parity_unsubscribe", unsubscribe.Method);
            Assert.Equal("0xabcd", unsubscribe.RawParameters[0]);
        }
    }
}
