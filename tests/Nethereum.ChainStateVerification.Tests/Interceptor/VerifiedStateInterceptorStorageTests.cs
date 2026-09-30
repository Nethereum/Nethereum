using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ChainStateVerification;
using Nethereum.ChainStateVerification.Interceptor;
using Nethereum.Consensus.LightClient;
using Nethereum.Model;
using Xunit;

namespace Nethereum.ChainStateVerification.Tests.Interceptor
{
    public class VerifiedStateInterceptorStorageTests
    {
        private sealed class StubVerifiedStateService : IVerifiedStateService
        {
            public byte[] StorageValueToReturn;
            public VerificationMode Mode { get; set; }

            public Task<Account> GetAccountAsync(string address) => Task.FromResult<Account>(null);
            public Task<BigInteger> GetBalanceAsync(string address) => Task.FromResult(BigInteger.Zero);
            public Task<BigInteger> GetNonceAsync(string address) => Task.FromResult(BigInteger.Zero);
            public Task<byte[]> GetCodeAsync(string address) => Task.FromResult<byte[]>(null);
            public Task<byte[]> GetCodeHashAsync(string address) => Task.FromResult<byte[]>(null);

            public Task<byte[]> GetStorageAtAsync(string address, BigInteger position)
                => Task.FromResult(StorageValueToReturn);

            public Task<byte[]> GetStorageAtAsync(string address, string slotHex)
                => Task.FromResult(StorageValueToReturn);

            public byte[] GetBlockHash(ulong blockNumber) => null;

            public TrustedExecutionHeader GetCurrentHeader() => null;
        }

        [Fact]
        public async Task HandleGetStorageAtAsync_TrimmedLeadingZeroValue_ReturnsFull32BytePaddedHex()
        {
            var service = new StubVerifiedStateService { StorageValueToReturn = new byte[] { 0x01 } };
            var interceptor = new VerifiedStateInterceptor(service);

            var result = await interceptor.InterceptSendRequestAsync<string>(
                (method, route, paramList) => Task.FromResult<string>(null),
                "eth_getStorageAt",
                null,
                "0x0000000000000000000000000000000000000001",
                "0x1");

            Assert.Equal(
                "0x0000000000000000000000000000000000000000000000000000000000000001",
                result);
        }

        [Fact]
        public async Task HandleGetStorageAtAsync_EmptySlot_ReturnsCanonicalZero32Bytes()
        {
            var service = new StubVerifiedStateService { StorageValueToReturn = System.Array.Empty<byte>() };
            var interceptor = new VerifiedStateInterceptor(service);

            var result = await interceptor.InterceptSendRequestAsync<string>(
                (method, route, paramList) => Task.FromResult<string>(null),
                "eth_getStorageAt",
                null,
                "0x0000000000000000000000000000000000000001",
                "0x1");

            Assert.Equal(
                "0x0000000000000000000000000000000000000000000000000000000000000000",
                result);
        }
    }
}
