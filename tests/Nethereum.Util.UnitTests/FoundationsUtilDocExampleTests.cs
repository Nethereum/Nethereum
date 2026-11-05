using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Documentation;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util.Rest;
using Xunit;

namespace Nethereum.Util.UnitTests
{
    public class FoundationsUtilDocExampleTests
    {
        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-backoff", "Backoff doubles a wait to a cap and resets")]
        public void Given_ABackoff_When_AdvancedPastItsCap_Then_ItStopsAtTheCapAndResetReturnsTheInitialWait()
        {
            var backoff = new Backoff(100);

            backoff.Advance(1000);
            Assert.Equal(200, backoff.Current);
            backoff.Advance(1000);
            backoff.Advance(1000);
            backoff.Advance(1000);
            backoff.Advance(1000);
            Assert.Equal(1000, backoff.Current);

            backoff.Reset();
            Assert.Equal(100, backoff.Current);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-prefix-successor", "ByteUtil.PrefixSuccessor bounds a prefix scan")]
        public void Given_APrefix_When_ItsSuccessorIsTaken_Then_ItIsTheExclusiveUpperBoundOfEveryKeyWithThatPrefix()
        {
            var prefix = new byte[] { 0x12, 0xFF };

            var upperBound = ByteUtil.PrefixSuccessor(prefix);

            Assert.Equal(new byte[] { 0x13, 0x00 }, upperBound);
            Assert.True(ByteArrayComparer.Current.Compare(new byte[] { 0x12, 0xFF, 0xFF, 0xFF }, upperBound) < 0);
            Assert.Null(ByteUtil.PrefixSuccessor(new byte[] { 0xFF, 0xFF }));
            Assert.Null(ByteUtil.PrefixSuccessor(null));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-byte-array-comparer", "ByteArrayComparer as an equality comparer")]
        public void Given_TwoByteArraysWithTheSameContent_When_UsedAsDictionaryKeys_Then_TheyAreTheSameKey()
        {
            var map = new System.Collections.Generic.Dictionary<byte[], string>(ByteArrayComparer.Current);

            map[new byte[] { 1, 2, 3 }] = "first";
            map[new byte[] { 1, 2, 3 }] = "second";

            Assert.Single(map);
            Assert.False(ByteArrayComparer.Current.Equals(new byte[] { 1, 2 }, new byte[] { 1, 2, 0 }));
            Assert.Equal(0, ByteArrayComparer.Current.GetHashCode(null));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-create2-bytes", "CREATE2 address from byte arrays")]
        public void Given_AnAddressSaltAndInitCode_When_TheCreate2AddressIsCalculatedFromBytes_Then_ItMatchesEip1014()
        {
            var deployer = "0x0000000000000000000000000000000000000000";
            var salt = new byte[32];
            var initCode = new byte[] { 0x00 };

            var fromBytes = ContractUtils.CalculateCreate2Address(deployer, salt, initCode);
            var fromHex = ContractUtils.CalculateCreate2Address(deployer, salt.ToHex(true), initCode.ToHex(true));

            Assert.Equal("0x4D1A2e2bB4F88F0250f26Ffff098B0b30B26BF38", fromBytes);
            Assert.Equal(fromHex, fromBytes);
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-evmuint256-slice", "EvmUInt256.FromBigEndian on a slice of a buffer")]
        public void Given_ABufferHoldingAWord_When_ASliceIsRead_Then_ItIsTheBigEndianValueAndAShortSliceIsZeroExtended()
        {
            var buffer = new byte[] { 0xAA, 0x01, 0x02, 0x03, 0xBB };

            var value = EvmUInt256.FromBigEndian(buffer, 1, 3);
            var padded = EvmUInt256.FromBigEndian(new byte[] { 0x01, 0x02 }, 0, 4);

            Assert.Equal(new BigInteger(0x010203), (BigInteger)value);
            Assert.Equal(new BigInteger(0x0102) << 16, (BigInteger)padded);
            Assert.Throws<ArgumentOutOfRangeException>(() => EvmUInt256.FromBigEndian(buffer, 0, 33));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-keccak-into-buffer", "Keccak-256 of a slice into an output buffer")]
        public void Given_AnInputSlice_When_HashedIntoAnOutputBuffer_Then_TheDigestIsWrittenAtTheOffset()
        {
            var sha3 = new Sha3Keccack();
            var input = Encoding.UTF8.GetBytes("xxhello world");
            var output = new byte[40];

            sha3.CalculateHash(input, 2, 11, output, 4);

            var expected = sha3.CalculateHash(Encoding.UTF8.GetBytes("hello world"));
            Assert.Equal(expected, output.Skip(4).Take(32).ToArray());
            Assert.Throws<ArgumentOutOfRangeException>(() => sha3.CalculateHash(input, 0, input.Length, output, 20));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-evmaddress-fromhex", "EvmAddress.FromHex parses a 40-digit address")]
        public void Given_AnAddressWithAndWithoutThePrefix_When_ParsedWithFromHex_Then_BothGiveTheSameTwentyBytes()
        {
            var prefixed = EvmAddress.FromHex("0xD8dA6BF26964aF9D7eEd9e03E53415D37aA96045");
            var bare = EvmAddress.FromHex("d8da6bf26964af9d7eed9e03e53415d37aa96045");

            Assert.Equal(prefixed, bare);
            Assert.Equal(20, prefixed.ToByteArray().Length);
            Assert.Equal(EvmAddress.Zero, EvmAddress.FromHex(null));
        }

        [Fact]
        [NethereumDocExample(DocSection.CoreFoundation, "util-rest-response-cap", "RestHttpHelper rejects a response over MaxResponseBytes")]
        public async Task Given_AResponseLargerThanTheCap_When_RequestedThroughRestHttpHelper_Then_AnHttpRequestExceptionIsThrown()
        {
            var handler = new FixedResponseHandler(new string('a', 2048));
            var restHelper = new RestHttpHelper(new HttpClient(handler)) { MaxResponseBytes = 1024 };

            await Assert.ThrowsAsync<HttpRequestException>(() => restHelper.GetAsync<string>("https://example.test/data"));
            Assert.Equal(32L * 1024 * 1024, RestHttpHelper.DefaultMaxResponseBytes);
        }

        private sealed class FixedResponseHandler : HttpMessageHandler
        {
            private readonly string _body;

            public FixedResponseHandler(string body)
            {
                _body = body;
            }

            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_body, Encoding.UTF8, "application/json")
                });
            }
        }
    }
}
