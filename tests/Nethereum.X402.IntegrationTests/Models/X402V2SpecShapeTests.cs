using Nethereum.X402.Facilitator;
using Nethereum.X402.IntegrationTests.Client;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;
using System.Net;
using System.Text.Json;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.Models;

public class X402V2SpecShapeTests
{
    [Fact]
    public void Given_SupportedResponse_When_Serialized_Then_KindsExtensionsAndSignersArePresent()
    {
        var response = new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind>
            {
                new() { X402Version = 2, Scheme = "exact", Network = "eip155:84532" }
            },
            Signers = new Dictionary<string, List<string>> { ["eip155:*"] = new() { "0xabc" } }
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response));
        var root = json.RootElement;

        Assert.Equal("exact", root.GetProperty("kinds")[0].GetProperty("scheme").GetString());
        Assert.Equal(JsonValueKind.Array, root.GetProperty("extensions").ValueKind);
        Assert.Equal(0, root.GetProperty("extensions").GetArrayLength());
        Assert.Equal("0xabc", root.GetProperty("signers").GetProperty("eip155:*")[0].GetString());
    }

    [Fact]
    public void Given_DefaultSupportedResponse_When_Created_Then_ExtensionsAndSignersAreEmptyNotNull()
    {
        var response = new SupportedPaymentKindsResponse();

        Assert.NotNull(response.Extensions);
        Assert.Empty(response.Extensions);
        Assert.NotNull(response.Signers);
        Assert.Empty(response.Signers);
    }

    [Fact]
    public void Given_ResourceInfoWithOptionalFields_When_Serialized_Then_ServiceNameTagsAndIconUrlUseSpecNames()
    {
        var resource = new ResourceInfo
        {
            Url = "https://api.example.com/x",
            ServiceName = "svc",
            Tags = new List<string> { "a", "b" },
            IconUrl = "https://api.example.com/i.png"
        };

        using var json = JsonDocument.Parse(JsonSerializer.Serialize(resource));
        var root = json.RootElement;

        Assert.Equal("svc", root.GetProperty("serviceName").GetString());
        Assert.Equal(2, root.GetProperty("tags").GetArrayLength());
        Assert.Equal("https://api.example.com/i.png", root.GetProperty("iconUrl").GetString());
    }

    [Fact]
    public void Given_ResourceInfoWithoutOptionalFields_When_Serialized_Then_TheyAreOmitted()
    {
        var json = JsonSerializer.Serialize(new ResourceInfo { Url = "https://api.example.com/x" });

        Assert.DoesNotContain("serviceName", json);
        Assert.DoesNotContain("tags", json);
        Assert.DoesNotContain("iconUrl", json);
    }

    [Fact]
    public void Given_VerificationResponseWithoutPayer_When_Serialized_Then_PayerIsOmitted()
    {
        var json = JsonSerializer.Serialize(new VerificationResponse { IsValid = false, InvalidReason = "x" });

        Assert.DoesNotContain("payer", json);
        Assert.DoesNotContain("extensions", json);
        Assert.DoesNotContain("extra", json);
    }

    [Fact]
    public void Given_VerificationResponseWithExtensionsAndExtra_When_Serialized_Then_BothAreWritten()
    {
        var json = JsonSerializer.Serialize(new VerificationResponse
        {
            IsValid = true,
            Payer = "0xabc",
            Extensions = new Dictionary<string, string> { ["k"] = "v" },
            Extra = new Dictionary<string, string> { ["e"] = "v" }
        });

        Assert.Contains("\"extensions\":{\"k\":\"v\"}", json);
        Assert.Contains("\"extra\":{\"e\":\"v\"}", json);
    }

    [Fact]
    public void Given_SettlementResponseWithoutPayerOrTransaction_When_Serialized_Then_PayerIsOmittedAndTransactionIsEmptyString()
    {
        var json = JsonSerializer.Serialize(new SettlementResponse
        {
            Success = false,
            ErrorReason = X402ErrorCodes.UnexpectedSettleError,
            Network = "eip155:84532"
        });

        Assert.DoesNotContain("payer", json);
        Assert.Contains("\"transaction\":\"\"", json);
    }

    [Fact]
    public async Task Given_VerifyRequest_When_PostedByHttpFacilitatorClient_Then_BodyCarriesX402Version2()
    {
        var body = await CaptureBodyAsync(
            "/verify", new VerificationResponse { IsValid = true, Payer = "0xabc" },
            client => client.VerifyAsync(CreatePayload(), CreateRequirements()));

        Assert.Equal(2, body.RootElement.GetProperty("x402Version").GetInt32());
        Assert.True(body.RootElement.TryGetProperty("paymentPayload", out _));
        Assert.True(body.RootElement.TryGetProperty("paymentRequirements", out _));
    }

    [Fact]
    public async Task Given_SettleRequest_When_PostedByHttpFacilitatorClient_Then_BodyCarriesX402Version2()
    {
        var body = await CaptureBodyAsync(
            "/settle", new SettlementResponse { Success = true, Transaction = "0x1", Network = "eip155:84532" },
            client => client.SettleAsync(CreatePayload(), CreateRequirements()));

        Assert.Equal(2, body.RootElement.GetProperty("x402Version").GetInt32());
    }

    [Fact]
    public void Given_FacilitatorRequestModels_When_Created_Then_X402VersionDefaultsToProtocolVersion()
    {
        Assert.Equal(X402Protocol.Version, new FacilitatorVerifyRequest().X402Version);
        Assert.Equal(X402Protocol.Version, new FacilitatorSettleRequest().X402Version);
    }

    [Fact]
    public void Given_ErrorCodes_When_Read_Then_UnsupportedSchemeIsTheSpecCodeAndNewCodesAreDefined()
    {
        Assert.Equal("unsupported_scheme", X402ErrorCodes.UnsupportedScheme);
        Assert.Equal("invalid_exact_evm_payload_authorization_value_mismatch", X402ErrorCodes.AuthorizationValueMismatch);
        Assert.Equal("invalid_exact_evm_transfer_event_mismatch", X402ErrorCodes.TransferEventMismatch);
        Assert.Equal("settlement_pending", X402ErrorCodes.SettlementPending);
    }

    [Theory]
    [InlineData(null, null, true, "eip3009", true)]
    [InlineData(null, "eip3009", true, "eip3009", true)]
    [InlineData(null, "permit2", true, "permit2", true)]
    [InlineData("authorization", "permit2", true, "permit2", true)]
    [InlineData(null, "erc7710", true, "erc7710", false)]
    [InlineData("upfront", null, false, "eip3009", true)]
    [InlineData("upfront", "permit2", false, "permit2", true)]
    [InlineData("escrow", null, false, "eip3009", false)]
    public void Given_RequirementExtra_When_Classified_Then_FlowMethodAndSupportFollowTheSpec(
        string? paymentFlow, string? assetTransferMethod, bool isAuthorizationFlow, string method, bool supported)
    {
        var requirements = new PaymentRequirements
        {
            Scheme = "exact",
            Extra = new ExactSchemeExtra { PaymentFlow = paymentFlow, AssetTransferMethod = assetTransferMethod }
        };

        Assert.Equal(isAuthorizationFlow, X402RequirementSupport.IsAuthorizationFlow(requirements));
        Assert.Equal(method, X402RequirementSupport.ResolveAssetTransferMethod(requirements));
        Assert.Equal(supported, X402RequirementSupport.IsSupportedByThisLibrary(requirements));
    }

    [Fact]
    public void Given_RequirementReceivedAsJson_When_Classified_Then_FlowAndMethodDoNotDependOnNameAndVersion()
    {
        var extra = JsonSerializer.Deserialize<JsonElement>("{\"assetTransferMethod\":\"permit2\",\"paymentFlow\":\"authorization\"}");
        var requirements = new PaymentRequirements { Scheme = "exact", Extra = extra };

        Assert.Equal("permit2", X402RequirementSupport.ResolveAssetTransferMethod(requirements));
        Assert.True(X402RequirementSupport.IsSupportedByThisLibrary(requirements));
    }

    [Fact]
    public void Given_NonExactScheme_When_Classified_Then_ItIsNotSupportedByThisLibrary()
    {
        Assert.False(X402RequirementSupport.IsSupportedByThisLibrary(new PaymentRequirements { Scheme = "upto" }));
    }

    [Fact]
    public async Task Given_TwoProcessorsSupportingTheSameSigner_When_MergingSupported_Then_KindsAndSignersAreUnionedCaseInsensitively()
    {
        var eip3009 = new StubSupportedProcessor(new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind> { new() { X402Version = 2, Scheme = "exact", Network = "eip155:1" } },
            Extensions = new List<string> { "a" },
            Signers = new Dictionary<string, List<string>> { ["eip155:*"] = new() { "0xAbC" } }
        });
        var permit2 = new StubSupportedProcessor(new SupportedPaymentKindsResponse
        {
            Kinds = new List<PaymentKind> { new() { X402Version = 2, Scheme = "exact", Network = "eip155:1" } },
            Extensions = new List<string> { "a", "b" },
            Signers = new Dictionary<string, List<string>> { ["eip155:*"] = new() { "0xabc", "0xdef" } }
        });

        var merged = await new X402ExactSchemeProcessor(eip3009, permit2).GetSupportedAsync();

        Assert.Single(merged.Kinds);
        Assert.Equal(new[] { "a", "b" }, merged.Extensions);
        Assert.Equal(new[] { "0xAbC", "0xdef" }, merged.Signers["eip155:*"]);
    }

    private static async Task<JsonDocument> CaptureBodyAsync<T>(
        string path, T response, Func<HttpFacilitatorClient, Task> call)
    {
        string? captured = null;
        var handler = new MockHttpMessageHandler(async (request, ct) =>
        {
            Assert.EndsWith(path, request.RequestUri!.AbsolutePath);
            captured = await request.Content!.ReadAsStringAsync(ct);
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(response))
            };
        });

        await call(new HttpFacilitatorClient(new HttpClient(handler), "https://facilitator.example.com"));

        return JsonDocument.Parse(captured!);
    }

    private static PaymentPayload CreatePayload() => new()
    {
        Accepted = CreateRequirements(),
        Payload = new ExactSchemePayload
        {
            Signature = "0x00",
            Authorization = new Authorization
            {
                From = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266",
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "1",
                ValidAfter = "0",
                ValidBefore = "1",
                Nonce = "0x00"
            }
        }
    };

    private static PaymentRequirements CreateRequirements() => new()
    {
        Scheme = "exact",
        Network = "eip155:84532",
        Amount = "1",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        MaxTimeoutSeconds = 60
    };

    private sealed class StubSupportedProcessor : IX402PaymentProcessor
    {
        private readonly SupportedPaymentKindsResponse _supported;

        public StubSupportedProcessor(SupportedPaymentKindsResponse supported) => _supported = supported;

        public Task<VerificationResponse> VerifyPaymentAsync(
            PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SettlementResponse> SettlePaymentAsync(
            PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException();

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(_supported);
    }
}
