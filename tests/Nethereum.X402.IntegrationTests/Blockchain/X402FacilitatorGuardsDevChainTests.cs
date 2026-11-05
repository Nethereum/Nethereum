using System.Numerics;
using Nethereum.DevChain;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402FacilitatorGuardsDevChainTests : IClassFixture<X402DevChainFixture>, IAsyncLifetime
{
    private const int ChainId = X402DevChainFixture.ChainId;
    private const string FacilitatorKey = X402DevChainFixture.FacilitatorKey;
    private static readonly string Network = Caip2.FormatEip155(ChainId);

    private readonly X402DevChainFixture _chain;
    private readonly EthECKey _payer = new(X402DevChainFixture.PayerKey.Substring(2));

    public X402FacilitatorGuardsDevChainTests(X402DevChainFixture chain)
    {
        _chain = chain;
    }

    public async Task InitializeAsync()
    {
        await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        await _chain.Node.SetCodeAsync(GasBurningContracts.TokenAddress, GasBurningContracts.BurnsGasOnEveryCallExceptBalanceAndAuthorizationState);
        await X402Permit2DevChain.ApprovePermit2Async(_chain.Node, _payer, ChainId, _chain.TokenAddress);
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_ATokenWhoseTransferWithAuthorizationBurnsGas_When_Settling_Then_SettlementGasExceedsLimitAndNoTransactionIsSent()
    {
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(_chain.Client));
        var requirements = X402TokenPayments.Requirements(GasBurningContracts.TokenAddress);
        var payload = await X402TokenPayments.SignedTransferAsync(GasBurningContracts.TokenAddress, requirements);
        var before = await FacilitatorTransactionCountAsync();

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.False(settle.Success);
        Assert.Equal(X402ErrorCodes.SettlementGasExceedsLimit, settle.ErrorReason);
        Assert.Equal(before, await FacilitatorTransactionCountAsync());
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_ATokenWhoseTransferWithAuthorizationBurnsGas_When_Verifying_Then_SettlementGasExceedsLimit()
    {
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(_chain.Client));
        var requirements = X402TokenPayments.Requirements(GasBurningContracts.TokenAddress);
        var payload = await X402TokenPayments.SignedTransferAsync(GasBurningContracts.TokenAddress, requirements);

        var verify = await service.VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.SettlementGasExceedsLimit, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AProxyWhoseSettleBurnsGas_When_SettlingAPermit2Payment_Then_SettlementGasExceedsLimitAndNoTransactionIsSent()
    {
        await _chain.Node.SetCodeAsync(X402Permit2Addresses.ExactPermit2Proxy, GasBurningContracts.BurnsGasOnEveryCallExceptBalanceAndAuthorizationState);
        try
        {
            var service = new X402ExactPermit2Service(FacilitatorKey, Clients(_chain.Client));
            var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);
            var before = await FacilitatorTransactionCountAsync();

            var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

            Assert.False(settle.Success);
            Assert.Equal(X402ErrorCodes.SettlementGasExceedsLimit, settle.ErrorReason);
            Assert.True(string.IsNullOrEmpty(settle.Transaction));
            Assert.Equal(before, await FacilitatorTransactionCountAsync());
        }
        finally
        {
            await X402Permit2DevChain.InstallPermit2StubsAsync(_chain.Node);
        }
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_ACeilingBelowWhatARealTransferNeeds_When_Settling_Then_SettlementGasExceedsLimit()
    {
        var service = new X402TransferWithAuthorisation3009Service(
            FacilitatorKey, Clients(_chain.Client), new X402FacilitatorOptions { MaxSettlementGas = 21_000 });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.Equal(X402ErrorCodes.SettlementGasExceedsLimit, settle.ErrorReason);
    }

    [Theory]
    [Trait("Rule", "V2")]
    [InlineData(500_000L)]
    [InlineData(150_000L)]
    public async Task Given_AnEip3009Settlement_When_ItIsSent_Then_ItsExplicitGasLimitCoversTheEstimateAndIsAtMostTheCeiling(long ceiling)
    {
        var service = new X402TransferWithAuthorisation3009Service(
            FacilitatorKey, Clients(_chain.Client), new X402FacilitatorOptions { MaxSettlementGas = ceiling });
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var settle = await service.SettlePaymentAsync(payload, requirements);

        Assert.True(settle.Success, settle.ErrorReason);
        await AssertGasLimitWithinAsync(settle.Transaction, ceiling);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_APermit2Settlement_When_ItIsSent_Then_ItsExplicitGasLimitIsAtMostTheCeiling()
    {
        var service = new X402ExactPermit2Service(
            FacilitatorKey, Clients(_chain.Client), (Erc20ApprovalGasSponsoringOptions?)null,
            new X402FacilitatorOptions { MaxSettlementGas = 200_000 });
        var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

        var settle = await service.SettlePaymentAsync(payload, payload.Accepted);

        Assert.False(string.IsNullOrEmpty(settle.Transaction), settle.ErrorReason);
        await AssertGasLimitWithinAsync(settle.Transaction, 200_000);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AnAssetAllowlistForTheNetwork_When_Verifying_AnEip3009PaymentForAnotherAsset_Then_ItIsRejectedBeforeAnyRpc()
    {
        var rpc = new InterceptingRpcClient(_chain.Client);
        var options = new X402FacilitatorOptions();
        options.AllowedAssets[Network] = new List<string> { "0x036CbD53842c5426634e7929541eC2318f3dCF7e" };
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(rpc), options);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var verify = await service.VerifyPaymentAsync(payload, requirements);

        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.AssetNotAllowed, verify.InvalidReason);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AnAssetAllowlistForTheNetwork_When_Verifying_APermit2PaymentForAnotherAsset_Then_ItIsRejectedBeforeAnyRpc()
    {
        var rpc = new InterceptingRpcClient(_chain.Client);
        var options = new X402FacilitatorOptions();
        options.AllowedAssets[Network] = new List<string> { "0x036CbD53842c5426634e7929541eC2318f3dCF7e" };
        var service = new X402ExactPermit2Service(FacilitatorKey, Clients(rpc), (Erc20ApprovalGasSponsoringOptions?)null, options);
        var payload = X402Permit2DevChain.Payload(_payer, ChainId, _chain.TokenAddress);

        var verify = await service.VerifyPaymentAsync(payload, payload.Accepted);

        Assert.Equal(X402ErrorCodes.AssetNotAllowed, verify.InvalidReason);
        Assert.Empty(rpc.Methods);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AnAssetAllowlistListingTheAssetInAnotherCase_When_Verifying_Then_ThePaymentIsAccepted()
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets[Network.ToUpperInvariant()] = new List<string> { _chain.TokenAddress.ToLowerInvariant() };
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(_chain.Client), options);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var verify = await service.VerifyPaymentAsync(payload, requirements);

        Assert.True(verify.IsValid, verify.InvalidReason);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AnAllowlistConfiguredOnlyForAnotherNetwork_When_Verifying_Then_ThisNetworkIsUnrestricted()
    {
        var options = new X402FacilitatorOptions();
        options.AllowedAssets["eip155:8453"] = new List<string> { "0x833589fCD6eDb6E08f4c7C32D4f71b54bdA02913" };
        var service = new X402TransferWithAuthorisation3009Service(FacilitatorKey, Clients(_chain.Client), options);
        var requirements = X402DevChainPayments.Requirements(_chain, "1000000", X402DevChainFixture.RecipientAddress);
        var payload = await X402DevChainPayments.SignedTransfer(_chain, requirements);

        var verify = await service.VerifyPaymentAsync(payload, requirements);

        Assert.True(verify.IsValid, verify.InvalidReason);
    }

    [Theory]
    [Trait("Rule", "V2")]
    [InlineData(0L, 1, 1)]
    [InlineData(-5L, 1, 1)]
    [InlineData(500_000L, 0, 1)]
    [InlineData(500_000L, 1, 0)]
    public void Given_InvalidFacilitatorOptions_When_Validated_Then_TheyAreRejected(long maxGas, int confirmations, int maxReceiptWait)
    {
        var options = new X402FacilitatorOptions
        {
            MaxSettlementGas = maxGas,
            RequiredConfirmations = confirmations,
            MaxReceiptWaitSeconds = maxReceiptWait
        };

        Assert.Throws<InvalidOperationException>(options.Validate);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public void Given_NewFacilitatorOptions_Then_TheCeilingIs500000AndOneConfirmationIsRequired()
    {
        var options = new X402FacilitatorOptions();

        Assert.Equal(500_000L, options.MaxSettlementGas);
        Assert.Equal(1, options.RequiredConfirmations);
        Assert.Equal(3600, options.MaxReceiptWaitSeconds);
        Assert.Null(options.AuthorizationPolicy);
        Assert.Empty(options.AllowedAssets);
    }

    private async Task AssertGasLimitWithinAsync(string transactionHash, long ceiling)
    {
        var web3 = _chain.Node.CreateWeb3();
        var transaction = await web3.Eth.Transactions.GetTransactionByHash.SendRequestAsync(transactionHash);
        var receipt = await web3.Eth.Transactions.GetTransactionReceipt.SendRequestAsync(transactionHash);

        Assert.True(transaction.Gas.Value <= ceiling, $"gas limit {transaction.Gas.Value} above the ceiling {ceiling}");
        Assert.True(transaction.Gas.Value >= receipt.GasUsed.Value, $"gas limit {transaction.Gas.Value} below the gas used {receipt.GasUsed.Value}");
        Assert.NotEqual(new BigInteger(30_000_000), transaction.Gas.Value);
    }

    private Task<BigInteger> FacilitatorTransactionCountAsync() =>
        X402Permit2DevChain.TransactionCountAsync(_chain.Node, X402DevChainFixture.FacilitatorAddress);

    private static Dictionary<int, IClient> Clients(IClient client) => new() { [ChainId] = client };
}
