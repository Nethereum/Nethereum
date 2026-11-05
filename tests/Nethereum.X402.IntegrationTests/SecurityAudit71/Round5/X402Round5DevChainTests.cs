using System.Numerics;
using Nethereum.JsonRpc.Client;
using Nethereum.JsonRpc.Client.RpcMessages;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.SecurityAudit71.Round5;

[Collection("X402 DevChain E2E")]
public class X402Round5DevChainTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private static readonly BigInteger GoodMaxFee = 1_000_000_000;
    private static readonly BigInteger ApprovalGasCost = 70_000 * GoodMaxFee;

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _fundedPayer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402Round5DevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    public async Task InitializeAsync()
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        await X402Permit2DevChain.ApprovePermit2Async(_chain.Node, _fundedPayer, ChainId, _chain.TokenAddress);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    private sealed class LostReplyAfterBroadcastClient : IClient
    {
        private readonly IClient _inner;

        public LostReplyAfterBroadcastClient(IClient inner)
        {
            _inner = inner;
        }

        public RequestInterceptor OverridingRequestInterceptor
        {
            get => _inner.OverridingRequestInterceptor;
            set => _inner.OverridingRequestInterceptor = value;
        }

        public T DecodeResult<T>(RpcResponseMessage rpcResponseMessage) => _inner.DecodeResult<T>(rpcResponseMessage);

        public Task SendRequestAsync(RpcRequest request, string? route = null) => _inner.SendRequestAsync(request, route);

        public Task SendRequestAsync(string method, string? route = null, params object[] paramList) =>
            _inner.SendRequestAsync(method, route, paramList);

        public Task<T> SendRequestAsync<T>(RpcRequest request, string? route = null) =>
            ForwardThenLose(request.Method, () => _inner.SendRequestAsync<T>(request, route));

        public Task<T> SendRequestAsync<T>(string method, string? route = null, params object[] paramList) =>
            ForwardThenLose(method, () => _inner.SendRequestAsync<T>(method, route, paramList));

        public Task<RpcResponseMessage> SendAsync(RpcRequestMessage rpcRequestMessage, string? route = null) =>
            _inner.SendAsync(rpcRequestMessage, route);

        public Task<RpcRequestResponseBatch> SendBatchRequestAsync(RpcRequestResponseBatch rpcRequestResponseBatch) =>
            _inner.SendBatchRequestAsync(rpcRequestResponseBatch);

        private static async Task<T> ForwardThenLose<T>(string method, Func<Task<T>> forward)
        {
            var result = await forward();
            if (method == InterceptingRpcClient.SendRawMethod)
                throw new HttpRequestException(HttpRequestError.ResponseEnded, "reset", new IOException("connection reset"));

            return result;
        }
    }

    [Fact(Timeout = 120000)]
    [Trait("Rule", "R5-3")]
    public async Task Given_TheSponsoredApprovalWasBroadcastButTheReplyWasLost_When_Settling_Then_TheOutcomeIsPendingNotADefiniteBroadcastFailure()
    {
        var client = new LostReplyAfterBroadcastClient(_chain.Client);
        var nonce = await X402Permit2DevChain.TransactionCountAsync(_chain.Node, _fundedPayer.GetPublicAddress());
        var facilitator = new Nethereum.Web3.Web3(new Account(X402DevChainFixture.FacilitatorKey, ChainId), client);
        var spec = new SignedApprovalSpec(ChainId, _chain.TokenAddress, nonce, GoodMaxFee);
        var request = new Erc20ApprovalSponsoredSettlementRequest(
            facilitator,
            Caip2.FormatEip155(ChainId),
            _fundedPayer.GetPublicAddress(),
            Erc20ApprovalTransactions.Sign(_fundedPayer, spec),
            ApprovalGasCost,
            _ => Task.FromResult(new SettlementResponse { Success = true, Transaction = "0x01" }))
        {
            MaxTimeoutSeconds = 2
        };

        var result = await new Erc20ApprovalSponsoredSettlement().SettleAsync(request).WaitAsync(TimeSpan.FromSeconds(60));

        Assert.Equal(X402ErrorCodes.SettlementPending, result.ErrorReason);
    }
}
