using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Signers;

public class TransferWithAuthorisationSignerTests
{
    private const string TestPrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const string TestAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
    private const string TokenName = "USDC";
    private const string TokenVersion = "2";
    private const int ChainId = 84532;
    private const string VerifyingContract = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";

    [Fact]
    public async Task SignWithPrivateKeyAsync_ProducesValidSignature()
    {
        var signer = new TransferWithAuthorisationSigner();
        var authorization = CreateTestAuthorization();

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        Assert.NotNull(signature);
        Assert.NotNull(signature.V);
        Assert.NotNull(signature.R);
        Assert.NotNull(signature.S);
        Assert.Equal(32, signature.R.Length);
        Assert.Equal(32, signature.S.Length);
    }

    [Fact]
    public async Task RecoverAddress_MatchesSignerAddress()
    {
        var signer = new TransferWithAuthorisationSigner();
        var authorization = CreateTestAuthorization();

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        var recoveredAddress = signer.RecoverAddress(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            signature
        );

        Assert.True(
            TestAddress.IsTheSameAddress(recoveredAddress),
            $"Expected {TestAddress}, got {recoveredAddress}"
        );
    }

    [Fact]
    public async Task RecoverAddress_WithDifferentAuthorization_ReturnsDifferentAddress()
    {
        var signer = new TransferWithAuthorisationSigner();
        var authorization = CreateTestAuthorization();

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        var modifiedAuthorization = CreateTestAuthorization();
        modifiedAuthorization.Value = "2000000";

        var recoveredAddress = signer.RecoverAddress(
            modifiedAuthorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            signature
        );

        Assert.False(
            TestAddress.IsTheSameAddress(recoveredAddress),
            "Modified authorization should not recover to same address"
        );
    }

    [Fact]
    public async Task SignWithPrivateKeyAsync_VValueIsValid()
    {
        var signer = new TransferWithAuthorisationSigner();
        var authorization = CreateTestAuthorization();

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        var v = signature.V[0];
        Assert.True(v == 27 || v == 28, $"V value should be 27 or 28, got {v}");
    }

    [Fact]
    public async Task SignWithPrivateKeyAsync_ProducesDeterministicSignature()
    {
        var signer = new TransferWithAuthorisationSigner();
        var authorization = CreateTestAuthorization();

        var signature1 = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        var signature2 = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        Assert.Equal(signature1.R, signature2.R);
        Assert.Equal(signature1.S, signature2.S);
        Assert.Equal(signature1.V, signature2.V);
    }

    [Fact]
    public async Task RecoverAddress_WithDifferentDomain_ReturnsDifferentAddress()
    {
        var signer = new TransferWithAuthorisationSigner();
        var authorization = CreateTestAuthorization();

        var signature = await signer.SignWithPrivateKeyAsync(
            authorization,
            TokenName,
            TokenVersion,
            ChainId,
            VerifyingContract,
            TestPrivateKey
        );

        var recoveredAddress = signer.RecoverAddress(
            authorization,
            "DifferentToken",
            TokenVersion,
            ChainId,
            VerifyingContract,
            signature
        );

        Assert.False(
            TestAddress.IsTheSameAddress(recoveredAddress),
            "Different domain should not recover to same address"
        );
    }

    private Authorization CreateTestAuthorization()
    {
        return new Authorization
        {
            From = TestAddress,
            To = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8",
            Value = "1000000",
            ValidAfter = "0",
            ValidBefore = DateTimeOffset.UtcNow.AddHours(1).ToUnixTimeSeconds().ToString(),
            Nonce = Guid.NewGuid().ToByteArray().Concat(Guid.NewGuid().ToByteArray()).Take(32).ToArray().ToHex(true)
        };
    }
}
