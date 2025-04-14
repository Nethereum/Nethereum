using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Geth.IntegrationTests.Testers;
using Nethereum.Geth.RPC.Debug;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.Debug.Tracers;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugTraceTransactionTester : RPCRequestTester<JToken>, IRPCRequestTester
    {
        
        public static IEnumerable<object[]> GetCallTracerConfigCombinations() => Utils.GetBooleanCombinations(2);
        public static IEnumerable<object[]> GetOpcodeTracerConfigCombinations() => Utils.GetBooleanCombinations(5);

        public string GetError(JToken result)
        {
            var structsLogs = (JArray) result["structLogs"];
            var lastCall = structsLogs[structsLogs.Count - 1];
            return lastCall["error"].Value<string>();
        }

        //Live error ""0x022f440fa96eb469363804d7b6c52321d4f409fa76578cdbdc5f04ff494b1321" one call
        //Live error "0x2bf8b77737953752535380c87a443de4974899f97a84fddf04a7764330f9964c"
        //Live normal = "0x58c8e6eaab928b2ce991d4b949027d168d010ed9ac6b79d65bfb1c7495a89b7a"

        public override async Task<JToken> ExecuteAsync(IClient client)
        {
            var debugTraceTransaction = new DebugTraceTransaction(client);
            return await debugTraceTransaction.SendRequestAsync(
                Settings.GetTransactionHash(),
                new TracingOptions()).ConfigureAwait(false);
        }
        
        public async Task<TOutput> ExecuteAsync<TOutput>(IClient client, TracingOptions tracingOptions)
        {
            var debugTraceTransaction = new DebugTraceTransaction(client);
            return await debugTraceTransaction.SendRequestAsync<TOutput>(
                Settings.GetTransactionHash(),
                tracingOptions).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceTransaction);
        }

        [Fact]
        public async void ShouldReturnAJObjectRepresentingTheTransactionStack()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
            try
            {
                var error = GetError(result);
            }
            catch
            {
                //error is for a specific environment
            }
        }
        

        [Theory]
        [MemberData(nameof(GetCallTracerConfigCombinations))]
        public async void ShouldReturnCallTracerRpcResponse(bool onlyTopCalls, bool withLogs)
        {
            var result = await ExecuteAsync<CallTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new CallTracerInfo(onlyTopCalls, withLogs)
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturn4ByteTracerRpcResponse()
        {
            var result = await ExecuteAsync<FourByteTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new FourByteTracerInfo()
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnBigramTracerRpcResponse()
        {
            var result = await ExecuteAsync<BigramTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new BigramTracerInfo()
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnUnigramTracerRpcResponse()
        {
            var result = await ExecuteAsync<UnigramTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new UnigramTracerInfo()
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnTrigramTracerRpcResponse()
        {
            var result = await ExecuteAsync<TrigramTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new TrigramTracerInfo()
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnOpcountTracerRpcResponse()
        {
            var result = await ExecuteAsync<long>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new OpcountTracerInfo()
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Theory]
        [MemberData(nameof(GetOpcodeTracerConfigCombinations))]
        public async void ShouldReturnOpcodeTracerRpcResponse(
            bool enableMemory, 
            bool disableStack, 
            bool disableStorage, 
            bool enableReturnData,
            bool debug)
        {
            var limit = 10;
            var result = await ExecuteAsync<OpcodeTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new OpcodeTracerInfo(enableMemory, disableStack, disableStorage, enableReturnData, debug, limit)
                }).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnPrestateTracerRpcResponse()
        {
            var resultPrestateMode = await ExecuteAsync<PrestateTracerResponsePrestateMode>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new PrestateTracerInfo(false)
                }).ConfigureAwait(false);
            Assert.NotNull(resultPrestateMode);
            
            var resultDiffMode = await ExecuteAsync<PrestateTracerResponseDiffMode>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new PrestateTracerInfo(true)
                }).ConfigureAwait(false);
            Assert.NotNull(resultDiffMode);
        }
        
        [Fact]
        public async void ShouldReturnAJTokenForCustomTracer()
        {
            
            var result = await ExecuteAsync<JToken>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                    TracerInfo = new CustomTracerInfo(Utils.GetCustomTracerCode()) 
                }
                ).ConfigureAwait(false);
            Assert.NotNull(result);
            
        }
        
        [Fact]
        public async void ShouldUseOpcodeTracerIfNotSpecified()
        {
            
            var result = await ExecuteAsync<OpcodeTracerResponse>(
                Client,
                new TracingOptions()
                {
                    Timeout = "1m",
                    Reexec = 128,
                }
            ).ConfigureAwait(false);
            Assert.NotNull(result);
            
        }
        

        /*{"structLogs": [
{
"pc": 0,
"op": "PUSH1",
"gas": 62269,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [],
"memory": null,
"storage": {}
},
{
"pc": 2,
"op": "PUSH1",
"gas": 62266,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000060"
],
"memory": null,
"storage": {}
},
{
"pc": 4,
"op": "MSTORE",
"gas": 62254,
"gasCost": 12,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000060",
"0000000000000000000000000000000000000000000000000000000000000040"
],
"memory": [
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000"
],
"storage": {}
},
{
"pc": 5,
"op": "PUSH1",
"gas": 62251,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [],
"memory": [
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000060"
],
"storage": {}
},
{
"pc": 7,
"op": "DUP1",
"gas": 62248,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000072"
],
"memory": [
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000060"
],
"storage": {}
},
{
"pc": 8,
"op": "PUSH1",
"gas": 62245,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000072",
"0000000000000000000000000000000000000000000000000000000000000072"
],
"memory": [
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000060"
],
"storage": {}
},
{
"pc": 10,
"op": "PUSH1",
"gas": 62242,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000072",
"0000000000000000000000000000000000000000000000000000000000000072",
"0000000000000000000000000000000000000000000000000000000000000010"
],
"memory": [
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000060"
],
"storage": {}
},
{
"pc": 12,
"op": "CODECOPY",
"gas": 62224,
"gasCost": 18,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000072",
"0000000000000000000000000000000000000000000000000000000000000072",
"0000000000000000000000000000000000000000000000000000000000000010",
"0000000000000000000000000000000000000000000000000000000000000000"
],
"memory": [
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000000",
"0000000000000000000000000000000000000000000000000000000000000060",
"0000000000000000000000000000000000000000000000000000000000000000"
],
"storage": {}
},
{
"pc": 13,
"op": "PUSH1",
"gas": 62221,
"gasCost": 3,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000072"
],
"memory": [
"60606040526000357c0100000000000000000000000000000000000000000000",
"00000000000090048063c6888fa1146037576035565b005b604b600480803590",
"60200190919050506061565b6040518082815260200191505060405180910390",
"f35b6000600782029050606d565b919050560000000000000000000000000000"
],
"storage": {}
},
{
"pc": 15,
"op": "RETURN",
"gas": 62221,
"gasCost": 0,
"depth": 1,
"error": "",
"stack": [
"0000000000000000000000000000000000000000000000000000000000000072",
"0000000000000000000000000000000000000000000000000000000000000000"
],
"memory": [
"60606040526000357c0100000000000000000000000000000000000000000000",
"00000000000090048063c6888fa1146037576035565b005b604b600480803590",
"60200190919050506061565b6040518082815260200191505060405180910390",
"f35b6000600782029050606d565b919050560000000000000000000000000000"
],
"storage": {}
}
]}*/
    }
}