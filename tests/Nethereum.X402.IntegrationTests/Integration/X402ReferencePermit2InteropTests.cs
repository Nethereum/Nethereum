using System;
using System.Numerics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Extensions;
using Nethereum.Signer;
using Nethereum.Web3.Accounts;
using Nethereum.X402.IntegrationTests.Helpers;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Integration;

[Collection("X402 Reference Forward Interop")]
public class X402ReferencePermit2InteropTests
{
    private const int ChainId = 84532;

    private static string? RefUrl => Environment.GetEnvironmentVariable("X402_REF_FACILITATOR");
    private static string Rpc => Environment.GetEnvironmentVariable("X402_RPC") ?? "http://127.0.0.1:8545";

    [Fact]
    public async Task OurPermit2Payload_IsVerifiedAndSettledByReferenceFacilitator()
    {
        var refUrl = RefUrl;
        if (string.IsNullOrEmpty(refUrl))
            return;

        var deployerKey = EthECKey.GenerateKey();
        var payerKey = EthECKey.GenerateKey();
        var deployer = new Account(deployerKey.GetPrivateKey(), ChainId);
        var payer = new Account(payerKey.GetPrivateKey(), ChainId);
        var payTo = EthECKey.GenerateKey().GetPublicAddress();

        var web3 = new Nethereum.Web3.Web3(deployer, Rpc);
        var oneHundredEth = new HexBigInteger(BigInteger.Parse("100000000000000000000"));
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(deployer.Address, oneHundredEth);
        await web3.Eth.Anvil().SetBalance.SendRequestAsync(payer.Address, oneHundredEth);

        var usdc = new USDCDeploymentHelper(web3, deployer);
        var token = await usdc.DeployAsync("USDC", "USDC", 6, "2");
        await usdc.MintAsync(payer.Address, BigInteger.Parse("10000000"));

        var payerWeb3 = new Nethereum.Web3.Web3(payer, Rpc);
        await payerWeb3.Eth.ERC20.GetContractService(token)
            .ApproveRequestAndWaitForReceiptAsync(X402Permit2Addresses.Permit2, BigInteger.Pow(2, 96));

        var amount = BigInteger.Parse("1000000");
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Network = "eip155:84532",
            Amount = amount.ToString(),
            Asset = token,
            PayTo = payTo,
            MaxTimeoutSeconds = 3600,
            Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2" }
        };

        var nonceBytes = new byte[32];
        RandomNumberGenerator.Fill(nonceBytes);
        var nonce = new BigInteger(nonceBytes, isUnsigned: true, isBigEndian: true);
        var now = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var deadline = new BigInteger(now + 3600);
        var validAfter = new BigInteger(now - 600);

        var message = new PermitWitnessTransferFrom
        {
            Permitted = new TokenPermissions { Token = token, Amount = amount },
            Spender = X402Permit2Addresses.ExactPermit2Proxy,
            Nonce = nonce,
            Deadline = deadline,
            Witness = new Witness { To = payTo, ValidAfter = validAfter }
        };
        var signature = new Permit2WitnessSigner().Sign(message, ChainId, X402Permit2Addresses.Permit2, payerKey);

        var payload = new PaymentPayload
        {
            X402Version = 2,
            Accepted = requirements,
            Payload = new Permit2SchemePayload
            {
                Signature = signature,
                Permit2Authorization = new Permit2Authorization
                {
                    From = payer.Address,
                    Permitted = new Permit2TokenPermissions { Token = token, Amount = amount.ToString() },
                    Spender = X402Permit2Addresses.ExactPermit2Proxy,
                    Nonce = nonce.ToString(),
                    Deadline = deadline.ToString(),
                    Witness = new Permit2WitnessData { To = payTo, ValidAfter = validAfter.ToString() }
                }
            }
        };

        var body = JsonSerializer.Serialize(new { paymentPayload = payload, paymentRequirements = requirements });
        using var http = new System.Net.Http.HttpClient { BaseAddress = new Uri(refUrl) };

        var verifyResp = await http.PostAsync("/verify", new StringContent(body, Encoding.UTF8, "application/json"));
        var verifyJson = await verifyResp.Content.ReadAsStringAsync();
        Assert.True(verifyResp.IsSuccessStatusCode, $"/verify HTTP {verifyResp.StatusCode}: {verifyJson}");
        using (var doc = JsonDocument.Parse(verifyJson))
            Assert.True(doc.RootElement.GetProperty("isValid").GetBoolean(),
                $"reference facilitator rejected our permit2 payload: {verifyJson}");

        var settleResp = await http.PostAsync("/settle", new StringContent(body, Encoding.UTF8, "application/json"));
        var settleJson = await settleResp.Content.ReadAsStringAsync();
        Assert.True(settleResp.IsSuccessStatusCode, $"/settle HTTP {settleResp.StatusCode}: {settleJson}");
        using (var doc = JsonDocument.Parse(settleJson))
            Assert.True(doc.RootElement.GetProperty("success").GetBoolean(),
                $"reference facilitator failed to settle our permit2 payload: {settleJson}");

        var payToBalance = await usdc.GetBalanceAsync(payTo);
        Assert.Equal(amount, payToBalance);
    }
}
