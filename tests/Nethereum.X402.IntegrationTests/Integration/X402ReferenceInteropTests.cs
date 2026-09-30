using System;
using System.Net.Http;
using System.Numerics;
using System.Text;
using System.Text.Json;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.Blockchain;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Signers;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[CollectionDefinition("X402 Reference Forward Interop", DisableParallelization = true)]
public class X402ReferenceForwardInteropCollection { }

[Collection("X402 Reference Forward Interop")]
public class X402ReferenceInteropTests
{
    private const int ChainId = 84532;
    private const string TokenName = "USD Coin";
    private const string TokenVersion = "2";

    private static string? RefUrl => Environment.GetEnvironmentVariable("X402_REF_FACILITATOR");
    private static string Rpc => Environment.GetEnvironmentVariable("X402_RPC") ?? "http://127.0.0.1:8545";

    [Fact]
    public async Task OurClientPayload_IsVerifiedAndSettledByReferenceFacilitator()
    {
        var refUrl = RefUrl;
        if (string.IsNullOrEmpty(refUrl))
            return;

        var deployerKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);
        var payerAddress = payerKey.GetPublicAddress();
        var payToAddress = EthECKey.GenerateKey().GetPublicAddress();

        var web3 = new Nethereum.Web3.Web3(deployer, Rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(web3, deployer);
        var tokenAddress = await usdc.DeployAsync(TokenName, "USDC", 6, TokenVersion);
        await usdc.MintAsync(payerAddress, BigInteger.Parse("10000000"));

        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = Caip2.FormatEip155(ChainId),
            Amount = "1000000",
            Asset = tokenAddress,
            PayTo = payToAddress,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra
            {
                AssetTransferMethod = "eip3009",
                Name = TokenName,
                Version = TokenVersion
            }
        };

        var builder = new TransferWithAuthorisationBuilder();
        var signer = new TransferWithAuthorisationSigner();
        var authorization = builder.BuildFromPaymentRequirements(requirements, payerAddress);
        var signature = await signer.SignWithPrivateKeyAsync(
            authorization, TokenName, TokenVersion, ChainId, tokenAddress, payerKey.GetPrivateKey());

        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new ExactSchemePayload
            {
                Authorization = authorization,
                Signature = EncodeSignature(signature)
            }
        };

        var body = JsonSerializer.Serialize(new { paymentPayload = payload, paymentRequirements = requirements });
        using var http = new HttpClient { BaseAddress = new Uri(refUrl) };

        var verifyResp = await http.PostAsync("/verify",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var verifyJson = await verifyResp.Content.ReadAsStringAsync();
        Assert.True(verifyResp.IsSuccessStatusCode, $"/verify HTTP {verifyResp.StatusCode}: {verifyJson}");
        using (var doc = JsonDocument.Parse(verifyJson))
        {
            Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean(),
                $"reference facilitator rejected our payload: {verifyJson}");
        }

        var settleResp = await http.PostAsync("/settle",
            new StringContent(body, Encoding.UTF8, "application/json"));
        var settleJson = await settleResp.Content.ReadAsStringAsync();
        Assert.True(settleResp.IsSuccessStatusCode, $"/settle HTTP {settleResp.StatusCode}: {settleJson}");
        using (var doc = JsonDocument.Parse(settleJson))
        {
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean(),
                $"reference facilitator failed to settle our payload: {settleJson}");
        }

        var payToBalance = await usdc.GetBalanceAsync(payToAddress);
        Assert.Equal(BigInteger.Parse("1000000"), payToBalance);
    }

    private static string EncodeSignature(EthECDSASignature signature)
    {
        var bytes = new byte[signature.R.Length + signature.S.Length + signature.V.Length];
        signature.R.CopyTo(bytes, 0);
        signature.S.CopyTo(bytes, signature.R.Length);
        signature.V.CopyTo(bytes, signature.R.Length + signature.S.Length);
        return bytes.ToHex(true);
    }
}
