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

/// <summary>
/// Integration tests for X402ReceiveWithAuthorisation3009Service.
/// Tests the ReceiveWithAuthorization pattern where the receiver submits transactions and pays gas.
/// </summary>
public class X402ReceiveWithAuthorisation3009ServiceTests
{
    private const string BaseSepoliaRpc = "https://sepolia.base.org";
    private const string UsdcAddress = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const int ChainId = 84532;
    private const string NetworkName = "base-sepolia";

    private const string PayerPrivateKey = "0x7580e6fc491f1c871f00a0fae31c2224c6aba908e116b8da44ee8cd927b990b0";
    private const string PayerAddress = "0x819961f93e6C808D932e723290450aA2686979C1";

    // Receiver is the one who submits the transaction
    private const string ReceiverPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string ReceiverAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

    [Fact]
    public async Task VerifyPaymentAsync_WithValidPayment_ReturnsValid()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        // Payer signs the authorization for the receiver to pull funds
        var signature = await signer.SignReceiveWithPrivateKeyAsync(
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

        // Act
        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.True(result.IsValid, $"Verification failed: {result.InvalidReason}");
        Assert.Null(result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithWrongReceiverAddress_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        // Create requirements with wrong receiver address
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = NetworkName,
            MaxAmountRequired = "100000",
            Asset = "USDC",
            PayTo = "0x0000000000000000000000000000000000000001", // Wrong address
            Resource = "/api/test",
            Description = "Test payment",
            MaxTimeoutSeconds = 3600
        };

        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
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

        // Act
        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("does not match receiver address", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithInsufficientBalance_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        // Unrealistically large amount
        var requirements = CreatePaymentRequirements("10000000000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
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

        // Act
        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains("Insufficient balance", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithInvalidSignature_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        // Modify authorization after signing to make signature invalid
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

        // Act
        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Invalid signature for ReceiveWithAuthorization", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithExpiredAuthorization_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");

        // Create expired authorization
        var expiredValidAfter = new BigInteger(DateTimeOffset.UtcNow.AddHours(-2).ToUnixTimeSeconds());
        var expiredValidBefore = new BigInteger(DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds());

        var authorization = builder.BuildFromPaymentRequirements(
            requirements,
            PayerAddress,
            expiredValidAfter,
            expiredValidBefore
        );

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
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

        // Act
        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Authorization expired", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task VerifyPaymentAsync_WithNotYetValidAuthorization_ReturnsInvalid()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");

        // Create authorization that's not yet valid
        var futureValidAfter = new BigInteger(DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds());
        var futureValidBefore = new BigInteger(DateTimeOffset.UtcNow.AddHours(2).ToUnixTimeSeconds());

        var authorization = builder.BuildFromPaymentRequirements(
            requirements,
            PayerAddress,
            futureValidAfter,
            futureValidBefore
        );

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
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

        // Act
        var result = await service.VerifyPaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.False(result.IsValid);
        Assert.Equal("Authorization not yet valid", result.InvalidReason);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task GetSupportedAsync_ReturnsBaseSepoliaSupport()
    {
        // Arrange
        var service = CreateService();

        // Act
        var result = await service.GetSupportedAsync();

        // Assert
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
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("10000"); // Small amount for settlement
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
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

        // Act
        var result = await service.SettlePaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.True(result.Success, $"Settlement failed: {result.ErrorReason}");
        Assert.Null(result.ErrorReason);
        Assert.NotNull(result.Transaction);
        Assert.StartsWith("0x", result.Transaction);
        Assert.Equal(NetworkName, result.Network);
        Assert.Equal(PayerAddress, result.Payer);
    }

    [Fact]
    public async Task SettlePaymentAsync_WithInvalidPayment_Fails()
    {
        // Arrange
        var service = CreateService();
        var builder = new ReceiveWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();

        var requirements = CreatePaymentRequirements("100000");
        var authorization = builder.BuildFromPaymentRequirements(requirements, PayerAddress);

        var signature = await signer.SignReceiveWithPrivateKeyAsync(
            authorization,
            "USDC",
            "2",
            ChainId,
            UsdcAddress,
            PayerPrivateKey
        );

        // Tamper with authorization to make it invalid
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

        // Act
        var result = await service.SettlePaymentAsync(paymentPayload, requirements);

        // Assert
        Assert.False(result.Success);
        Assert.NotNull(result.ErrorReason);
        Assert.Contains("Invalid signature for ReceiveWithAuthorization", result.ErrorReason);
    }

    private X402ReceiveWithAuthorisation3009Service CreateService()
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

        return new X402ReceiveWithAuthorisation3009Service(
            ReceiverPrivateKey,
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
            PayTo = ReceiverAddress, // Must match the receiver's address
            Resource = "/api/test",
            Description = "Test payment for ReceiveWithAuthorization",
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
