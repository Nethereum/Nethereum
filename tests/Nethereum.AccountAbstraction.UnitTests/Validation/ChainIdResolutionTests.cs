using System.Numerics;
using System.Reflection;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Validation;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class ChainIdResolutionTests
    {
        private const string EntryPoint = "0x0000000000000000000000000000000000000007";

        [Fact]
        public async Task GetChainIdAsync_NodeError_Propagates_DoesNotDefaultToOne()
        {
            var validator = CreateValidator(new StubClient(throwOnRequest: true));

            await Assert.ThrowsAnyAsync<System.Exception>(() => InvokeGetChainId(validator));
        }

        [Fact]
        public async Task GetChainIdAsync_HappyPath_ReturnsNodeChainId()
        {
            var validator = CreateValidator(new StubClient(throwOnRequest: false, chainIdHex: "0x2105"));

            var chainId = await InvokeGetChainId(validator);

            Assert.Equal(new BigInteger(8453), chainId);
        }

        private static UserOpValidator CreateValidator(IClient client)
        {
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { EntryPoint },
                BeneficiaryAddress = "0x3333333333333333333333333333333333333333",
                ChainId = null
            };

            return new UserOpValidator(new Web3.Web3(client), config);
        }

        private static Task<BigInteger> InvokeGetChainId(UserOpValidator validator)
        {
            var method = typeof(UserOpValidator).GetMethod(
                "GetChainIdAsync", BindingFlags.NonPublic | BindingFlags.Instance);
            Assert.NotNull(method);
            return (Task<BigInteger>)method!.Invoke(validator, null)!;
        }

        private sealed class StubClient : ClientBase
        {
            private readonly bool _throwOnRequest;
            private readonly string _chainIdHex;

            public StubClient(bool throwOnRequest, string chainIdHex = "0x1")
            {
                _throwOnRequest = throwOnRequest;
                _chainIdHex = chainIdHex;
            }

            public override Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string route = null)
            {
                if (_throwOnRequest)
                {
                    var error = new Nethereum.JsonRpc.Client.RpcMessages.RpcError
                    {
                        Code = -32000,
                        Message = "node unavailable"
                    };
                    return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, error));
                }

                return Task.FromResult(new RpcResponseMessage(rpcRequestMessage.Id, _chainIdHex));
            }

            protected override Task<RpcResponseMessage[]> SendAsync(RpcRequestMessage[] requests) =>
                throw new System.NotSupportedException();
        }
    }
}
