using System.Net;
using System.Numerics;
using System.Text.Json;
using Nethereum.ABI.EIP712.Permit2;
using Nethereum.Documentation;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.X402.Client;
using Nethereum.X402.Models;
using Nethereum.X402.Permit2;
using Xunit;

namespace Nethereum.X402.IntegrationTests.Client;

public class X402HttpClientPermit2Tests
{
    private const string PrivateKey = "0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
    private const int ChainId = 84532;
    private const string Network = "eip155:84532";
    private const string Token = "0x036CbD53842c5426634e7929541eC2318f3dCF7e";
    private const string PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C";
    private const string Amount = "100000";

    [Fact]
    [NethereumDocExample(DocSection.DeFi, "x402-payments", "Permit2 method: client builds a permit2 payload", Order = 41)]
    public async Task Given_Permit2Requirement_When_Paying_Then_ClientSignsVerifiablePermit2Payload()
    {
        PaymentPayload paid = null;
        var requestCount = 0;
        var handler = new MockHttpMessageHandler((request, ct) =>
        {
            requestCount++;
            if (requestCount == 1)
            {
                var required = new PaymentRequired
                {
                    Accepts = new List<PaymentRequirements>
                    {
                        new()
                        {
                            Scheme = "exact",
                            Network = Network,
                            Amount = Amount,
                            PayTo = PayTo,
                            MaxTimeoutSeconds = 300,
                            Asset = Token,
                            Extra = new ExactSchemeExtra { AssetTransferMethod = "permit2" }
                        }
                    }
                };
                return Task.FromResult(new HttpResponseMessage(HttpStatusCode.PaymentRequired)
                {
                    Content = new StringContent(JsonSerializer.Serialize(required))
                });
            }

            var header = request.Headers.GetValues("PAYMENT-SIGNATURE").First();
            paid = X402HttpClient.DecodePaymentHeader(header);
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("{\"ok\":true}")
            });
        });

        var client = new X402HttpClient(new HttpClient(handler), PrivateKey, new X402HttpClientOptions
        {
            PreferredNetwork = Network,
            PreferredScheme = "exact",
            MaxAmount = "1000000"
        });

        var response = await client.GetAsync("http://localhost/premium");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.NotNull(paid);

        var jsonElement = (JsonElement)paid.Payload;
        var payload = JsonSerializer.Deserialize<Permit2SchemePayload>(jsonElement.GetRawText());
        Assert.NotNull(payload.Permit2Authorization);
        var auth = payload.Permit2Authorization;

        var expectedPayer = new EthECKey(PrivateKey).GetPublicAddress();
        Assert.True(auth.From.IsTheSameAddress(expectedPayer));
        Assert.True(auth.Spender.IsTheSameAddress(X402Permit2Addresses.ExactPermit2Proxy));
        Assert.True(auth.Witness.To.IsTheSameAddress(PayTo));
        Assert.True(auth.Permitted.Token.IsTheSameAddress(Token));
        Assert.Equal(Amount, auth.Permitted.Amount);

        var message = new PermitWitnessTransferFrom
        {
            Permitted = new TokenPermissions { Token = auth.Permitted.Token, Amount = BigInteger.Parse(auth.Permitted.Amount) },
            Spender = auth.Spender,
            Nonce = BigInteger.Parse(auth.Nonce),
            Deadline = BigInteger.Parse(auth.Deadline),
            Witness = new Witness { To = auth.Witness.To, ValidAfter = BigInteger.Parse(auth.Witness.ValidAfter) }
        };
        var recovered = new Permit2WitnessSigner().RecoverSigner(message, ChainId, X402Permit2Addresses.Permit2, payload.Signature);
        Assert.True(recovered.IsTheSameAddress(expectedPayer));
    }
}
