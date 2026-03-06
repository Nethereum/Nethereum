using System.Net;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Nethereum.CoreChain.Storage;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.AppChain.Sync;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.AppChain.Sync.UnitTests
{
    public class HttpSequencerTxProxyTests
    {
        [Fact]
        public async Task SendRawTransactionAsync_ReturnsTransactionHash()
        {
            var expectedTxHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                var responseObj = new { jsonrpc = "2.0", id = 1, result = expectedTxHash };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", null, httpClient);

            var rawTx = new byte[] { 0x01, 0x02, 0x03 };
            var txHash = await proxy.SendRawTransactionAsync(rawTx);

            Assert.Equal(expectedTxHash.HexToByteArray(), txHash);
        }

        [Fact]
        public async Task SendRawTransactionAsync_ThrowsOnError()
        {
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                var responseObj = new { jsonrpc = "2.0", id = 1, error = new { code = -32000, message = "nonce too low" } };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", null, httpClient);

            var rawTx = new byte[] { 0x01, 0x02, 0x03 };

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                proxy.SendRawTransactionAsync(rawTx));

            Assert.Contains("nonce too low", ex.Message);
        }

        [Fact]
        public async Task GetTransactionReceiptAsync_ReturnsReceiptInfo()
        {
            var txHashHex = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
            var blockHashHex = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890";

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                var responseObj = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    result = new
                    {
                        status = "0x1",
                        cumulativeGasUsed = "0x5208",
                        logsBloom = "0x" + new string('0', 512),
                        logs = Array.Empty<object>(),
                        transactionHash = txHashHex,
                        blockHash = blockHashHex,
                        blockNumber = "0x10",
                        transactionIndex = "0x0",
                        gasUsed = "0x5208",
                        effectiveGasPrice = "0x3b9aca00"
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", null, httpClient);

            var txHash = txHashHex.HexToByteArray();
            var receiptInfo = await proxy.GetTransactionReceiptAsync(txHash);

            Assert.NotNull(receiptInfo);
            Assert.True(receiptInfo.Receipt.HasSucceeded);
            Assert.Equal(16, receiptInfo.BlockNumber);
            Assert.Equal(21000, (int)receiptInfo.GasUsed);
        }

        [Fact]
        public async Task GetTransactionReceiptAsync_ReturnsNullWhenNotFound()
        {
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                var responseObj = new { jsonrpc = "2.0", id = 1, result = (object?)null };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", null, httpClient);

            var txHash = new byte[32];
            var receiptInfo = await proxy.GetTransactionReceiptAsync(txHash);

            Assert.Null(receiptInfo);
        }

        [Fact]
        public async Task WaitForReceiptAsync_ChecksLocalStoreFirst()
        {
            var txHash = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef".HexToByteArray();
            var localReceiptStore = new InMemoryReceiptStore();

            var receipt = Receipt.CreateStatusReceipt(true, 21000, new byte[256], new List<Log>());
            await localReceiptStore.SaveAsync(receipt, txHash, new byte[32], 10, 0, 21000, null, 1000000000);

            var remoteRequestMade = false;
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                remoteRequestMade = true;
                var responseObj = new { jsonrpc = "2.0", id = 1, result = (object?)null };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", localReceiptStore, httpClient);

            var result = await proxy.WaitForReceiptAsync(txHash, timeoutMs: 1000, pollIntervalMs: 100);

            Assert.NotNull(result);
            Assert.Equal(10, result.BlockNumber);
            Assert.False(remoteRequestMade);
        }

        [Fact]
        public async Task WaitForReceiptAsync_FallsBackToRemote()
        {
            var txHashHex = "0x1234567890abcdef1234567890abcdef1234567890abcdef1234567890abcdef";
            var txHash = txHashHex.HexToByteArray();
            var localReceiptStore = new InMemoryReceiptStore();

            var remoteCallCount = 0;
            var mockHandler = new MockHttpMessageHandler(request =>
            {
                remoteCallCount++;
                var responseObj = new
                {
                    jsonrpc = "2.0",
                    id = 1,
                    result = new
                    {
                        status = "0x1",
                        cumulativeGasUsed = "0x5208",
                        logsBloom = "0x" + new string('0', 512),
                        logs = Array.Empty<object>(),
                        transactionHash = txHashHex,
                        blockHash = "0xabcdef1234567890abcdef1234567890abcdef1234567890abcdef1234567890",
                        blockNumber = "0x5",
                        transactionIndex = "0x0",
                        gasUsed = "0x5208",
                        effectiveGasPrice = "0x3b9aca00"
                    }
                };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", localReceiptStore, httpClient);

            var result = await proxy.WaitForReceiptAsync(txHash, timeoutMs: 5000, pollIntervalMs: 100);

            Assert.NotNull(result);
            Assert.Equal(5, result.BlockNumber);
            Assert.True(remoteCallCount >= 1);
        }

        [Fact]
        public async Task WaitForReceiptAsync_ReturnsNullOnTimeout()
        {
            var txHash = new byte[32];
            var localReceiptStore = new InMemoryReceiptStore();

            var mockHandler = new MockHttpMessageHandler(request =>
            {
                var responseObj = new { jsonrpc = "2.0", id = 1, result = (object?)null };
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(JsonSerializer.Serialize(responseObj), Encoding.UTF8, "application/json")
                };
            });

            var httpClient = new HttpClient(mockHandler);
            var proxy = new HttpSequencerTxProxy("http://localhost:8545", localReceiptStore, httpClient);

            var result = await proxy.WaitForReceiptAsync(txHash, timeoutMs: 500, pollIntervalMs: 100);

            Assert.Null(result);
        }
    }

    public class MockHttpMessageHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _handler;

        public MockHttpMessageHandler(Func<HttpRequestMessage, HttpResponseMessage> handler)
        {
            _handler = handler;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(_handler(request));
        }
    }
}
