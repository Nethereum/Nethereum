using System;
using System.Linq;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Signers;

public class TransferWithAuthorisationBuilderTests
{
    [Fact]
    public void GenerateNonce_Returns32Bytes()
    {
        var builder = new TransferWithAuthorisationBuilder();

        var nonce = builder.GenerateNonce();

        Assert.Equal(32, nonce.Length);
    }

    [Fact]
    public void GenerateNonce_ReturnsUniqueNonces()
    {
        var builder = new TransferWithAuthorisationBuilder();

        var nonce1 = builder.GenerateNonce();
        var nonce2 = builder.GenerateNonce();

        Assert.NotEqual(nonce1, nonce2);
    }

    [Fact]
    public void BuildFromPaymentRequirements_SetsCorrectAddresses()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var requirements = new PaymentRequirements
        {
            PayTo = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            MaxAmountRequired = "1000000"
        };
        var fromAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress);

        Assert.Equal(fromAddress, authorization.From);
        Assert.Equal(requirements.PayTo, authorization.To);
    }

    [Fact]
    public void BuildFromPaymentRequirements_SetsCorrectValue()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var requirements = new PaymentRequirements
        {
            PayTo = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            MaxAmountRequired = "1000000"
        };
        var fromAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress);

        Assert.Equal(requirements.MaxAmountRequired, authorization.Value);
    }

    [Fact]
    public void BuildFromPaymentRequirements_ValidAfterIsInPast()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var requirements = new PaymentRequirements
        {
            PayTo = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            MaxAmountRequired = "1000000"
        };
        var fromAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress);
        var validAfter = BigInteger.Parse(authorization.ValidAfter);
        var now = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        Assert.True(validAfter < now);
    }

    [Fact]
    public void BuildFromPaymentRequirements_ValidBeforeIsInFuture()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var requirements = new PaymentRequirements
        {
            PayTo = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            MaxAmountRequired = "1000000"
        };
        var fromAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress);
        var validBefore = BigInteger.Parse(authorization.ValidBefore);
        var now = new BigInteger(DateTimeOffset.UtcNow.ToUnixTimeSeconds());

        Assert.True(validBefore > now);
    }

    [Fact]
    public void BuildFromPaymentRequirements_NonceIs32BytesHex()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var requirements = new PaymentRequirements
        {
            PayTo = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            MaxAmountRequired = "1000000"
        };
        var fromAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";

        var authorization = builder.BuildFromPaymentRequirements(requirements, fromAddress);
        var nonceBytes = authorization.Nonce.HexToByteArray();

        Assert.Equal(32, nonceBytes.Length);
        Assert.StartsWith("0x", authorization.Nonce);
    }

    [Fact]
    public void BuildFromPaymentRequirements_UsesCustomTimestamps()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var requirements = new PaymentRequirements
        {
            PayTo = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            MaxAmountRequired = "1000000"
        };
        var fromAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        var customValidAfter = new BigInteger(1000);
        var customValidBefore = new BigInteger(9999);

        var authorization = builder.BuildFromPaymentRequirements(
            requirements,
            fromAddress,
            customValidAfter,
            customValidBefore
        );

        Assert.Equal("1000", authorization.ValidAfter);
        Assert.Equal("9999", authorization.ValidBefore);
    }

    [Fact]
    public void GetTypedDataForAuthorization_ReturnsCorrectDomain()
    {
        var builder = new TransferWithAuthorisationBuilder();
        var tokenName = "USDC";
        var tokenVersion = "2";
        var chainId = new BigInteger(84532);
        var verifyingContract = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";

        var typedData = builder.GetTypedDataForAuthorization(
            tokenName,
            tokenVersion,
            chainId,
            verifyingContract
        );

        Assert.Equal(tokenName, typedData.Domain.Name);
        Assert.Equal(tokenVersion, typedData.Domain.Version);
        Assert.Equal(chainId, typedData.Domain.ChainId);
        Assert.Equal(verifyingContract, typedData.Domain.VerifyingContract);
    }

    [Fact]
    public void GetTypedDataForAuthorization_ReturnsPrimaryTypeTransferWithAuthorization()
    {
        var builder = new TransferWithAuthorisationBuilder();

        var typedData = builder.GetTypedDataForAuthorization(
            "USDC",
            "2",
            new BigInteger(84532),
            "0x036CbD53842c5426634e7929541eC2318f3dCF7e"
        );

        Assert.Equal("TransferWithAuthorization", typedData.PrimaryType);
    }
}
