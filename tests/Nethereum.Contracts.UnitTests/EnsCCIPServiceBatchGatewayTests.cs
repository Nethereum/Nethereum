using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.Contracts;
using Nethereum.Contracts.Standards.ENS;
using Nethereum.Contracts.Standards.ENS.BatchGateway.ContractDefinition;
using Nethereum.Contracts.Standards.ENS.OffchainResolver.ContractDefinition;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.Contracts.UnitTests
{
    /// <summary>
    /// ENSIP-21 local batch gateway (BGOLP) handling. The batch is decoded and each inner EIP-3668 read
    /// runs through the same SSRF-validated per-URL fetch, so local handling cannot bypass the SSRF
    /// protection. Network is never reached: private/refused inner URLs throw during validation, and the
    /// external-path tests short-circuit the HTTP methods.
    /// </summary>
    public class EnsCCIPServiceBatchGatewayTests
    {
        private const string Sender = "0x1234567890123456789012345678901234567890";

        private class TestEnsCCIPService : EnsCCIPService
        {
            public string LastGetUrl;
            public string LastPostUrl;
            public bool CaptureHttp;

            public Task<string> FetchGatewayDataForTest(OffchainLookupError lookup) => FetchGatewayDataAsync(lookup);

            public override Task<T> HttpGet<T>(string url)
            {
                LastGetUrl = url;
                if (CaptureHttp) return Task.FromResult((T)(object)new CCIPReadResponse { Data = "0x" });
                return base.HttpGet<T>(url);
            }

            public override Task<TResponse> HttpPost<TRequest, TResponse>(string url, TRequest request)
            {
                LastPostUrl = url;
                if (CaptureHttp) return Task.FromResult((TResponse)(object)new CCIPReadResponse { Data = "0x" });
                return base.HttpPost<TRequest, TResponse>(url, request);
            }
        }

        private static byte[] BuildBatchCallData(params string[] innerUrls)
        {
            var requests = new List<BatchGatewayRequest>
            {
                new BatchGatewayRequest
                {
                    Sender = Sender,
                    Urls = new List<string>(innerUrls),
                    Data = new byte[] { 0x12, 0x34 }
                }
            };
            return new QueryFunction { Requests = requests }.GetCallData();
        }

        [Fact]
        public async Task LocalBatchGateway_RefusesPrivateInnerUrl_AsFailure()
        {
            var ccipService = new TestEnsCCIPService();
            var lookup = new OffchainLookupError
            {
                Sender = Sender,
                Urls = new List<string> { EnsCCIPService.LocalBatchGatewayUrl },
                CallData = BuildBatchCallData("https://10.0.0.1/gateway/{data}")
            };

            var resultHex = await ccipService.FetchGatewayDataForTest(lookup);
            var output = new QueryOutputDTO().DecodeOutput(resultHex);

            Assert.Single(output.Failures);
            Assert.True(output.Failures[0]);
            Assert.Empty(output.Responses[0]);
        }

        [Fact]
        public async Task ExternalBatchGateway_UsedWhenLocalDisabled()
        {
            var ccipService = new TestEnsCCIPService { AllowLocalBatchGateway = false, CaptureHttp = true };
            var lookup = new OffchainLookupError
            {
                Sender = Sender,
                Urls = new List<string> { "https://ccip-v3.ens.xyz", EnsCCIPService.LocalBatchGatewayUrl },
                CallData = new byte[] { 0xab, 0xcd }
            };

            await ccipService.FetchGatewayDataForTest(lookup);

            Assert.Equal("https://ccip-v3.ens.xyz", ccipService.LastPostUrl);
        }

        [Fact]
        public async Task LocalBatchGateway_UsedByDefault_DoesNotCallExternal()
        {
            var ccipService = new TestEnsCCIPService { CaptureHttp = true };
            var lookup = new OffchainLookupError
            {
                Sender = Sender,
                Urls = new List<string> { "https://ccip-v3.ens.xyz", EnsCCIPService.LocalBatchGatewayUrl },
                CallData = BuildBatchCallData("https://gateway.example/lookup/{data}")
            };

            await ccipService.FetchGatewayDataForTest(lookup);

            Assert.Null(ccipService.LastPostUrl);
            Assert.Contains("gateway.example", ccipService.LastGetUrl);
        }
    }
}
