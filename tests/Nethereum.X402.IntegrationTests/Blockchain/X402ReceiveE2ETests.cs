using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.JsonRpc.Client;
using Nethereum.Signer;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Nethereum.Documentation;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

[Collection("X402 DevChain E2E")]
public class X402ReceiveE2ETests : IClassFixture<X402DevChainFixture>
{
    private readonly X402DevChainFixture _chain;
    private readonly X402ReceiveWithAuthorisation3009Service _service;
    private readonly ReceiveWithAuthorisationBuilder _builder = new();
    private readonly TransferWithAuthorisationSigner _signer = new();

    public X402ReceiveE2ETests(X402DevChainFixture chain)
    {
        _chain = chain;
        _service = new X402ReceiveWithAuthorisation3009Service(
            X402DevChainFixture.FacilitatorKey,
            new Dictionary<int, IClient> { [X402DevChainFixture.ChainId] = chain.Client });
    }

    private PaymentRequirements Requirements(string amount, string? payTo = null) => new()
    {
        Scheme = "exact",
        Network = Caip2.FormatEip155(X402DevChainFixture.ChainId),
        Amount = amount,
        Asset = _chain.TokenAddress,
        PayTo = payTo ?? X402DevChainFixture.FacilitatorAddress,
        MaxTimeoutSeconds = 3600,
        Extra = new ExactSchemeExtra { Name = X402DevChainFixture.TokenName, Version = X402DevChainFixture.TokenVersion }
    };

    private async Task<PaymentPayload> SignedPayload(
        PaymentRequirements requirements,
        Action<Authorization>? tamper = null,
        BigInteger? validAfter = null,
        BigInteger? validBefore = null)
    {
        var authorization = _builder.BuildFromPaymentRequirements(
            requirements, X402DevChainFixture.PayerAddress, validAfter, validBefore);
        var signature = await _signer.SignReceiveWithPrivateKeyAsync(
            authorization, X402DevChainFixture.TokenName, X402DevChainFixture.TokenVersion,
            X402DevChainFixture.ChainId, _chain.TokenAddress, X402DevChainFixture.PayerKey);
        tamper?.Invoke(authorization);
        return new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = EncodeSignature(signature)
            }
        };
    }

    private static string EncodeSignature(EthECDSASignature signature)
    {
        var bytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(bytes, 0);
        signature.S.CopyTo(bytes, signature.R.Length);
        signature.V.CopyTo(bytes, signature.R.Length + signature.S.Length);
        return bytes.ToHex(true);
    }

    [Fact]
    [NethereumDocExample(DocSection.SmartContracts, "built-in-standards", "EIP-3009 ReceiveWithAuthorization: verify payment authorization", SkillName = "built-in-standards", Order = 12)]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Receive model: verify payment authorization", Order = 12)]
    public async Task ValidPayment_Verifies()
    {
        var requirements = Requirements("1000000");
        var payload = await SignedPayload(requirements);

        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.True(verify.IsValid, verify.InvalidReason);
        Assert.Null(verify.InvalidReason);
        Assert.Equal(X402DevChainFixture.PayerAddress, verify.Payer);
    }

    [Fact]
    public async Task RecipientMismatch_ReturnsRecipientMismatch()
    {
        var requirements = Requirements("1000000");
        var payload = await SignedPayload(Requirements("1000000", payTo: X402DevChainFixture.PayerAddress));

        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.RecipientMismatch, verify.InvalidReason);
    }

    [Fact]
    public async Task AmountBelowRequirement_ReturnsInvalidValue()
    {
        var requirements = Requirements("1000000");
        var payload = await SignedPayload(Requirements("500000"));

        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidValue, verify.InvalidReason);
    }

    [Fact]
    public async Task InsufficientBalance_ReturnsInsufficientFunds()
    {
        var requirements = Requirements("100000000");
        var payload = await SignedPayload(requirements);

        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InsufficientFunds, verify.InvalidReason);
    }

    [Fact]
    public async Task TamperedAuthorization_ReturnsInvalidSignature()
    {
        var requirements = Requirements("1000000");
        var payload = await SignedPayload(requirements, tamper: a => a.Nonce = "0x" + new string('1', 64));

        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidSignature, verify.InvalidReason);
    }

    [Fact]
    public async Task ExpiredAuthorization_ReturnsInvalidValidBefore()
    {
        var requirements = Requirements("1000000");
        var validAfter = new BigInteger(DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds());
        var validBefore = new BigInteger(DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());
        var payload = await SignedPayload(requirements, validAfter: validAfter, validBefore: validBefore);

        var verify = await _service.VerifyPaymentAsync(payload, requirements);
        Assert.False(verify.IsValid);
        Assert.Equal(X402ErrorCodes.InvalidValidBefore, verify.InvalidReason);
    }
}
