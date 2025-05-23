using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Geth.IntegrationTests.Testers;
using Nethereum.Geth.RPC.Debug;
using Nethereum.Geth.RPC.Debug.DTOs;
using Nethereum.Geth.RPC.Debug.Tracers;
using Nethereum.Hex.HexTypes;
using Nethereum.JsonRpc.Client;
using Nethereum.RPC.Tests.Testers;
using Newtonsoft.Json.Linq;
using Xunit; 
 // ReSharper disable ConsiderUsingConfigureAwait  
 // ReSharper disable AsyncConverter.ConfigureAwaitHighlighting

namespace Nethereum.Geth.Tests.Testers
{
    public class DebugTraceBlockByHashTester : RPCRequestTester<JArray>, IRPCRequestTester
    {
        public static IEnumerable<object[]> GetCallTracerConfigCombinations() => Utils.GetBooleanCombinations(2);

        public override async Task<JArray> ExecuteAsync(IClient client)
        {
            var debugTraceBlockByHash = new DebugTraceBlockByHash(client);
            //live block number 1700742
            return await debugTraceBlockByHash.SendRequestAsync(Settings.GetBlockHash(), new TracingCallOptions()).ConfigureAwait(false);
        }
        
        public async Task<BlockResponseDto<TOutput>> ExecuteAsync<TOutput>(IClient client, TracingCallOptions tracingCallOptions)
        {
            var debugTraceBlockByHash = new DebugTraceBlockByHash(client);
            return await debugTraceBlockByHash.SendRequestAsync<TOutput>(
                Settings.GetBlockHash(),
                tracingCallOptions).ConfigureAwait(false);
        }

        public override Type GetRequestType()
        {
            return typeof(DebugTraceBlockByHash);
        }

        [Fact]
        public async void ShouldDecodeTheBlockRplAsJObject()
        {
            var result = await ExecuteAsync().ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Theory]
        [MemberData(nameof(GetCallTracerConfigCombinations))]
        public async void ShouldReturnCallTracerRpcResponse(bool onlyTopCalls, bool withLogs)
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new CallTracerInfo(onlyTopCalls, withLogs);
            var result = await ExecuteAsync<CallTracerResponse>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturn4ByteTracerRpcResponse()
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new FourByteTracerInfo();
            var result = await ExecuteAsync<FourByteTracerResponse>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnBigramTracerRpcResponse()
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new BigramTracerInfo();
            var result = await ExecuteAsync<BigramTracerResponse>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnUnigramTracerRpcResponse()
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new UnigramTracerInfo();
            var result = await ExecuteAsync<UnigramTracerResponse>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnTrigramTracerRpcResponse()
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new TrigramTracerInfo();
            var result = await ExecuteAsync<TrigramTracerResponse>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnOpcountTracerRpcResponse()
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new OpcountTracerInfo();

            var result = await ExecuteAsync<long>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
        [Fact]
        public async void ShouldReturnPrestateTracerRpcResponse()
        {
            var configPrestateMode = Utils.GetBaseTracingCallConfig();
            configPrestateMode.TracerInfo = new PrestateTracerInfo(false);

            var resultPrestateMode = await ExecuteAsync<PrestateTracerResponsePrestateMode>(
                Client,
                configPrestateMode).ConfigureAwait(false);
            Assert.NotNull(resultPrestateMode);
            
            var configDiffMode = Utils.GetBaseTracingCallConfig();
            configDiffMode.TracerInfo = new PrestateTracerInfo(true);

            var resultDiffMode = await ExecuteAsync<PrestateTracerResponseDiffMode>(
                Client,
                configDiffMode).ConfigureAwait(false);
            Assert.NotNull(resultDiffMode);
        }
        
        [Fact]
        public async void ShouldReturnAJTokenForCustomTracer()
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new CustomTracerInfo(Utils.GetCustomTracerCode());
            var result = await ExecuteAsync<JToken>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
            
        }
        
        [Theory]
        [MemberData(nameof(GetCallTracerConfigCombinations))]
        public async void ShouldReturnCallTracerRpcResponseWithOverrides(bool onlyTopCalls, bool withLogs)
        {
            var config = Utils.GetBaseTracingCallConfig();
            config.TracerInfo = new CallTracerInfo(onlyTopCalls, withLogs);
            var result = await ExecuteAsync<CallTracerResponse>(
                Client,
                config).ConfigureAwait(false);
            Assert.NotNull(result);
        }
        
    }
}