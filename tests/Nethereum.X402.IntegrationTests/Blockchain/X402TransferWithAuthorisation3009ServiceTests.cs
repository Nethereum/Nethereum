using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Blockchain;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Blockchain;

public class X402TransferWithAuthorisation3009ServiceTests
{
    private const string BaseSepoliaRpc = "https://sepolia.base.org";
    private const string UsdcAddress = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const int ChainId = 84532;
    private const string NetworkName = "base-sepolia";

    private const string PayerPrivateKey = "0x7580e6fc491f1c871f00a0fae31c2224c6aba908e116b8da44ee8cd927b990b0";
    private const string PayerAddress = "0x819961f93e6C808D932e723290450aA2686979C1";
    private const string RecipientAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    private const string FacilitatorPrivateKey = "0x7580e6fc491f1c871f00a0fae31c2224c6aba908e116b8da44ee8cd927b990b0";

    [Fact]
    public async Task VerifyPaymentAsync_WithValidPayment_ReturnsValid()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        var signatureHex = EncodeSignature(signature);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = NetworkName,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = signatureHex
            }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.True(result.IsValid, $"Verification failed: {result.InvalidReason}");
        Assert.Null(result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithInsufficientBalance_ReturnsInvalid()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("10000000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        var signatureHex = EncodeSignature(signature);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = NetworkName,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = signatureHex
            }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Contains("Insufficient balance", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithInvalidSignature_ReturnsInvalid()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        authorization.Value = "200000";

        var signatureHex = EncodeSignature(signature);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = NetworkName,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = signatureHex
            }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Equal("Invalid signature", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithExpiredAuthorization_ReturnsInvalid()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");

        var expiredValidAfter = new BigInteger(DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds());
        var expiredValidBefore = new BigInteger(DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());

        var authorization = builder.BuildFromPaymentRequirements(
            requirements,
            PayerAddress,
            expiredValidAfter,
            expiredValidBefore
        );

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        var signatureHex = EncodeSignature(signature);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = NetworkName,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = signatureHex
            }
        };

        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        Assert.False(result.IsValid);
        Assert.Equal("Authorization expired", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task GetSupportedAsync_ReturnsBaseSepoliaSupport()
    {
        var service = CreateService();

        var result = await service.GetSupportedAsync();

        Assert.NotNull(result);
        Assert.NotEmpty(result.Kinds);
        Assert.Contains(result.Kinds, kind =>
            kind.X402Version == 1 &&
            kind.Scheme == "exact" &&
            kind.Network == NetworkName
        );
    }

    [Fact]
    public async Task SettlePaymentAsync_WithValidPayment_SucceedsOnChain()
    {
        var service = CreateService();
        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("10000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        var signatureHex = EncodeSignature(signature);

        var paymentPayload = new PaymentPayload
        {
            X402Version = 1,
            Scheme = "exact",
            Network = NetworkName,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = signatureHex
            }
        };

        var result = await service.SettlePaymentAsync(paymentPayload, requirements);

        Assert.True(result.Success, $"Settlement failed: {result.ErrorReason}");
        Assert.Null(result.ErrorReason);
        Assert.NotNull(result.Transaction);
        Assert.StartsWith("0x", result.Transaction);
        Assert.Equal(NetworkName, result.Network);
        Assert.Equal(PayerAddress, result.Payer);
    }

    private X402TransferWithAuthorisation3009Service CreateService()
    {
        var rpcEndpoints = new Dictionary<string, string>
        {
            [NetworkName] = BaseSepoliaRpc
        };

        var tokenAddresses = new Dictionary<string, string>
        {
            [NetworkName] = UsdcAddress
        };

        var chainIds = new Dictionary<string, int>
        {
            [NetworkName] = ChainId
        };

        var tokenNames = new Dictionary<string, string>
        {
            [NetworkName] = "USDC"
        };

        var tokenVersions = new Dictionary<string, string>
        {
            [NetworkName] = "2"
        };

        return new X402TransferWithAuthorisation3009Service(
            FacilitatorPrivateKey,
            rpcEndpoints,
            tokenAddresses,
            chainIds,
            tokenNames,
            tokenVersions
        );
    }

    private PaymentRequirements CreatePaymentRequirements(string amount)
    {
        return new PaymentRequirements
        {
            Scheme = "exact",
            Network = NetworkName,
            MaxAmountRequired = amount,
            Asset = "USDC",
            PayTo = RecipientAddress,
            Resource = "/api/test",
            Description = "Test payment",
            MaxTimeoutSeconds = 3600
        };
    }

    private string EncodeSignature(Nethereum.Signer.EthECDSASignature signature)
    {
        var signatureBytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(signatureBytes, 0);
        signature.S.CopyTo(signatureBytes, signature.R.Length);
        signature.V.CopyTo(signatureBytes, signature.R.Length + signature.S.Length);
        return signatureBytes.ToHex(true);
    }
}
