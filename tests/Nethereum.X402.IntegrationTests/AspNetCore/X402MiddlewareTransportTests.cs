using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.X402.AspNetCore;
using Nethereum.X402.Facilitator;
using Nethereum.X402.Models;
using Nethereum.X402.Server;
using System.Net;
using System.Text;
using System.Text.Json;
using Authorization = Nethereum.X402.Models.Authorization;

namespace Nethereum.X402.IntegrationTests.AspNetCore;

public class X402MiddlewareTransportTests
{
    private const string Payer = "0x857b06519E91e3A54538791bDbb0E22373e36b66";
    private const string PremiumBody = "premium data";
    private const string RouteNetwork = "eip155:84532";

    [Fact]
    public async Task Given_SettlementFails_When_Processing_Then_402CarriesPaymentResponseAndNoPaymentRequiredOrResourceBody()
    {
        var facilitator = new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = new SettlementResponse
            {
                Success = false,
                ErrorReason = X402ErrorCodes.InsufficientFunds,
                Transaction = null!,
                Network = null!,
                Payer = Payer
            }
        };

        var response = await PayAsync(facilitator);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(response.Headers.Contains(X402Headers.PaymentRequired));
        var settlement = DecodeSettlement(response);
        Assert.False(settlement.Success);
        Assert.Equal(X402ErrorCodes.InsufficientFunds, settlement.ErrorReason);
        Assert.Equal(string.Empty, settlement.Transaction);
        Assert.Equal(RouteNetwork, settlement.Network);
        Assert.Equal(Payer, settlement.Payer);
        Assert.Equal("{}", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_SettlementThrows_When_Processing_Then_402CarriesSettlementPendingForTheVerifiedPayer()
    {
        var facilitator = new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleException = new InvalidOperationException("facilitator down")
        };

        var response = await PayAsync(facilitator);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(response.Headers.Contains(X402Headers.PaymentRequired));
        var settlement = DecodeSettlement(response);
        Assert.False(settlement.Success);
        Assert.Equal(X402ErrorCodes.SettlementPending, settlement.ErrorReason);
        Assert.Equal(string.Empty, settlement.Transaction);
        Assert.Equal(RouteNetwork, settlement.Network);
        Assert.Equal(Payer, settlement.Payer);
        Assert.DoesNotContain(PremiumBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_SettlementPending_When_Processing_Then_TransactionHashReachesTheClientAndResourceIsWithheld()
    {
        const string hash = "0xabc0000000000000000000000000000000000000000000000000000000000def";
        var facilitator = new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = new SettlementResponse
            {
                Success = false,
                ErrorReason = X402ErrorCodes.SettlementPending,
                Transaction = hash,
                Network = RouteNetwork,
                Payer = Payer
            }
        };

        var response = await PayAsync(facilitator);

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(response.Headers.Contains(X402Headers.PaymentRequired));
        var settlement = DecodeSettlement(response);
        Assert.Equal(X402ErrorCodes.SettlementPending, settlement.ErrorReason);
        Assert.Equal(hash, settlement.Transaction);
        Assert.DoesNotContain(PremiumBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_PaymentSignatureThatDoesNotDecode_When_Processing_Then_400WithPaymentRequiredAndNoPaymentResponse()
    {
        var facilitator = new MockFacilitatorClient();
        using var server = CreateTestServer(facilitator);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(X402Headers.PaymentSignature, "invalid-base64!@#");

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(X402Headers.PaymentRequired));
        Assert.False(response.Headers.Contains(X402Headers.PaymentResponse));
        Assert.Contains(X402ErrorCodes.InvalidPayload, await response.Content.ReadAsStringAsync());
        Assert.False(facilitator.VerifyCalled);
    }

    [Fact]
    public async Task Given_PaymentSignatureWithMalformedJson_When_Processing_Then_400()
    {
        using var server = CreateTestServer(new MockFacilitatorClient());
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(
            X402Headers.PaymentSignature, Convert.ToBase64String(Encoding.UTF8.GetBytes("{not json")));

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(X402Headers.PaymentRequired));
    }

    [Fact]
    public async Task Given_VerifyReportsInvalidPayload_When_Processing_Then_400WithPaymentRequired()
    {
        var response = await PayAsync(InvalidVerification(X402ErrorCodes.InvalidPayload));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.True(response.Headers.Contains(X402Headers.PaymentRequired));
        Assert.False(response.Headers.Contains(X402Headers.PaymentResponse));
    }

    [Fact]
    public async Task Given_VerifyReportsPermit2AllowanceRequired_When_Processing_Then_412WithPaymentRequired()
    {
        var response = await PayAsync(InvalidVerification(X402ErrorCodes.Permit2AllowanceRequired));

        Assert.Equal(HttpStatusCode.PreconditionFailed, response.StatusCode);
        Assert.True(response.Headers.Contains(X402Headers.PaymentRequired));
        Assert.False(response.Headers.Contains(X402Headers.PaymentResponse));
    }

    [Fact]
    public async Task Given_VerifyFails_When_Processing_Then_402WithPaymentRequiredAndNoPaymentResponse()
    {
        var response = await PayAsync(InvalidVerification(X402ErrorCodes.InsufficientFunds));

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.True(response.Headers.Contains(X402Headers.PaymentRequired));
        Assert.False(response.Headers.Contains(X402Headers.PaymentResponse));
    }

    [Fact]
    public async Task Given_RequestWithoutPayment_When_Processing_Then_402WithoutPaymentResponse()
    {
        using var server = CreateTestServer(new MockFacilitatorClient());

        var response = await server.CreateClient().GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.True(response.Headers.Contains(X402Headers.PaymentRequired));
        Assert.False(response.Headers.Contains(X402Headers.PaymentResponse));
    }

    [Fact]
    public async Task Given_RouteResourceWithOptionalFields_When_402Generated_Then_ServiceNameTagsAndIconUrlAreReturned()
    {
        using var server = CreateTestServer(new MockFacilitatorClient(), new ResourceInfo
        {
            Url = "https://api.example.com/premium",
            ServiceName = "Premium API",
            Tags = new List<string> { "data", "paid" },
            IconUrl = "https://api.example.com/icon.png"
        });

        var response = await server.CreateClient().GetAsync("/api/premium");

        var required = JsonSerializer.Deserialize<PaymentRequired>(await response.Content.ReadAsStringAsync())!;
        Assert.Equal("Premium API", required.Resource.ServiceName);
        Assert.Equal(new[] { "data", "paid" }, required.Resource.Tags);
        Assert.Equal("https://api.example.com/icon.png", required.Resource.IconUrl);
    }

    [Fact]
    public void Given_ARouteWithAnUnknownFlow_When_Validating_Then_ItThrows()
    {
        var options = CreateOptions(new ExactSchemeExtra { PaymentFlow = "escrow", Name = "USDC", Version = "2" });

        var exception = Assert.Throws<InvalidOperationException>(() => options.Validate());

        Assert.Contains("paymentFlow 'escrow' is not supported", exception.Message);
        Assert.Contains("/api/premium", exception.Message);
    }

    [Fact]
    public void Given_RouteWithAuthorizationPaymentFlow_When_ValidatingOptions_Then_ValidationPasses()
    {
        var options = CreateOptions(new ExactSchemeExtra
        {
            PaymentFlow = X402PaymentFlows.Authorization,
            Name = "USDC",
            Version = "2"
        });

        options.Validate();
    }

    private static PaymentRequirements UpfrontRequirements() =>
        CreateRequirements(new ExactSchemeExtra { PaymentFlow = X402PaymentFlows.Upfront, Name = "USDC", Version = "2" });

    [Fact]
    public async Task Given_AnUpfrontRoute_When_PaidRequestArrives_Then_VerifyAndSettleRunBeforeTheHandler()
    {
        var facilitator = new MockFacilitatorClient { SettleResponse = SettlementResponseFor(success: true) };
        var settledWhenHandlerRan = false;

        var response = await PayAsync(facilitator, routeRequirements: UpfrontRequirements(), handler: context =>
        {
            settledWhenHandlerRan = facilitator.SettleCalled;
            return context.Response.WriteAsync(PremiumBody);
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(settledWhenHandlerRan);
        Assert.True(facilitator.VerifyCalled);
        Assert.True(DecodeSettlement(response).Success);
        Assert.Equal(PremiumBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_SettlementFails_Then_402WithPaymentResponseAndTheHandlerDoesNotRun()
    {
        var facilitator = new MockFacilitatorClient { SettleResponse = SettlementResponseFor(success: false) };
        var handlerRan = false;

        var response = await PayAsync(facilitator, routeRequirements: UpfrontRequirements(), handler: context =>
        {
            handlerRan = true;
            return context.Response.WriteAsync(PremiumBody);
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(handlerRan);
        Assert.True(facilitator.VerifyCalled);
        var settlement = DecodeSettlement(response);
        Assert.False(settlement.Success);
        Assert.Equal(X402ErrorCodes.InsufficientFunds, settlement.ErrorReason);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
        Assert.DoesNotContain(PremiumBody, await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_SettlementThrows_Then_402WithSettlementPendingAndTheHandlerDoesNotRun()
    {
        var facilitator = new MockFacilitatorClient { SettleException = new InvalidOperationException("facilitator down") };
        var handlerRan = false;

        var response = await PayAsync(facilitator, routeRequirements: UpfrontRequirements(), handler: context =>
        {
            handlerRan = true;
            return Task.CompletedTask;
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.False(handlerRan);
        Assert.Equal(X402ErrorCodes.SettlementPending, DecodeSettlement(response).ErrorReason);
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_TheHandlerFails_Then_ItsResponseCarriesTheSettlementReceiptAndIsPrivate()
    {
        var facilitator = new MockFacilitatorClient { SettleResponse = SettlementResponseFor(success: true) };

        var response = await PayAsync(facilitator, handlerCacheControl: "max-age=60", routeRequirements: UpfrontRequirements(), handler: context =>
        {
            context.Response.Headers["Cache-Control"] = "max-age=60";
            context.Response.StatusCode = StatusCodes.Status500InternalServerError;
            return context.Response.WriteAsync("handler failed");
        });

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var settlement = DecodeSettlement(response);
        Assert.True(settlement.Success);
        Assert.Equal("0x1234", settlement.Transaction);
        Assert.True(response.Headers.CacheControl!.Private);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.CacheControl.MaxAge);
        Assert.Equal("handler failed", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_TheHandlerThrowsAndTheHostMapsItTo500_Then_TheResponseCarriesTheSettlementReceiptAndIsPrivate()
    {
        var facilitator = new MockFacilitatorClient { SettleResponse = SettlementResponseFor(success: true) };
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(facilitator))
            .Configure(app =>
            {
                app.UseExceptionHandler(errors => errors.Run(context =>
                {
                    context.Response.StatusCode = StatusCodes.Status500InternalServerError;
                    return context.Response.WriteAsync("mapped");
                }));
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    options.Routes.Add(new RoutePaymentConfig("/api/premium", UpfrontRequirements()));
                });
                app.Run(_ => throw new InvalidOperationException("handler exploded"));
            });
        using var server = new TestServer(builder);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(X402Headers.PaymentSignature, EncodePaymentHeader(CreatePayment(UpfrontRequirements())));

        var response = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        Assert.True(DecodeSettlement(response).Success);
        Assert.True(response.Headers.CacheControl!.Private);
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_TheHandlerSucceeds_Then_TheResponseIsPrivate()
    {
        var response = await PayAsync(
            new MockFacilitatorClient { SettleResponse = SettlementResponseFor(success: true) },
            routeRequirements: UpfrontRequirements());

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.Private);
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_TheSamePaymentIsReplayed_Then_ItIsRejectedBeforeSettlingAgain()
    {
        var facilitator = new MockFacilitatorClient { SettleResponse = SettlementResponseFor(success: true) };
        var handlerRuns = 0;
        using var server = CreateTestServer(facilitator, routeRequirements: UpfrontRequirements(), handler: context =>
        {
            handlerRuns++;
            return context.Response.WriteAsync(PremiumBody);
        });
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(X402Headers.PaymentSignature, EncodePaymentHeader(CreatePayment(UpfrontRequirements())));

        var first = await client.GetAsync("/api/premium");
        var second = await client.GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(HttpStatusCode.PaymentRequired, second.StatusCode);
        Assert.Contains(X402ErrorCodes.NonceAlreadyUsed, await second.Content.ReadAsStringAsync());
        Assert.Equal(1, handlerRuns);
    }

    [Fact]
    public async Task Given_AnUpfrontRoute_When_ServingThe402_Then_AcceptsCarryPaymentFlowUpfront()
    {
        using var server = CreateTestServer(new MockFacilitatorClient(), routeRequirements: UpfrontRequirements());

        var response = await server.CreateClient().GetAsync("/api/premium");

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        var required = JsonSerializer.Deserialize<PaymentRequired>(await response.Content.ReadAsStringAsync())!;
        var extra = ExactSchemeExtra.FromRequirements(required.Accepts.Single())!;
        Assert.Equal(X402PaymentFlows.Upfront, extra.PaymentFlow);
    }

    [Theory]
    [InlineData(null)]
    [InlineData(X402AssetTransferMethods.Permit2)]
    public void Given_AnUpfrontRoute_When_Validating_Then_ItPasses(string? assetTransferMethod)
    {
        CreateOptions(new ExactSchemeExtra
        {
            PaymentFlow = X402PaymentFlows.Upfront,
            AssetTransferMethod = assetTransferMethod,
            Name = "USDC",
            Version = "2"
        }).Validate();
    }

    [Fact]
    public async Task Given_RequestWithoutPayment_When_Processing_Then_402IsNotStorable()
    {
        using var server = CreateTestServer(new MockFacilitatorClient());

        var response = await server.CreateClient().GetAsync("/api/premium");

        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Given_SettlementFails_When_Processing_Then_402IsNotStorable()
    {
        var response = await PayAsync(new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = SettlementResponseFor(success: false)
        });

        Assert.Equal(HttpStatusCode.PaymentRequired, response.StatusCode);
        Assert.Equal("no-store", response.Headers.CacheControl!.ToString());
    }

    [Fact]
    public async Task Given_SettledResponseWithoutCacheControl_When_Returned_Then_ItIsPrivate()
    {
        var response = await PayAsync(new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = SettlementResponseFor(success: true)
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.True(response.Headers.CacheControl!.Private);
    }

    [Fact]
    public async Task Given_SettledResponseWithHandlerCacheControl_When_Returned_Then_PrivateIsAddedAndTheHandlerDirectiveKept()
    {
        var response = await PayAsync(new MockFacilitatorClient
        {
            VerifyResponse = new VerificationResponse { IsValid = true, Payer = Payer },
            SettleResponse = SettlementResponseFor(success: true)
        }, handlerCacheControl: "max-age=60");

        Assert.True(response.Headers.CacheControl!.Private);
        Assert.Equal(TimeSpan.FromSeconds(60), response.Headers.CacheControl.MaxAge);
    }

    [Theory]
    [InlineData("A service name longer than 32 chars")]
    [InlineData("Café")]
    public void Given_RouteResourceWithInvalidServiceName_When_ValidatingOptions_Then_ValidationFails(string serviceName)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateOptions(resource: new ResourceInfo { ServiceName = serviceName }).Validate());

        Assert.Contains("ServiceName", exception.Message);
    }

    [Fact]
    public void Given_RouteResourceWithSixTags_When_ValidatingOptions_Then_ValidationFails()
    {
        var tags = new List<string> { "a", "b", "c", "d", "e", "f" };

        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateOptions(resource: new ResourceInfo { Tags = tags }).Validate());

        Assert.Contains("Tags", exception.Message);
    }

    [Theory]
    [InlineData("/icon.png")]
    [InlineData("ftp://api.example.com/icon.png")]
    public void Given_RouteResourceWithIconUrlThatIsNotAbsoluteHttp_When_ValidatingOptions_Then_ValidationFails(string iconUrl)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => CreateOptions(resource: new ResourceInfo { IconUrl = iconUrl }).Validate());

        Assert.Contains("IconUrl", exception.Message);
    }

    [Fact]
    public void Given_RouteResourceWithinTheSpecLimits_When_ValidatingOptions_Then_ValidationPasses()
    {
        CreateOptions(resource: new ResourceInfo
        {
            ServiceName = new string('s', 32),
            Tags = new List<string> { "a", "b", "c", "d", new string('t', 32) },
            IconUrl = "https://api.example.com/icon.png"
        }).Validate();
    }

    private static SettlementResponse SettlementResponseFor(bool success) => new()
    {
        Success = success,
        ErrorReason = success ? null : X402ErrorCodes.InsufficientFunds,
        Transaction = success ? "0x1234" : string.Empty,
        Network = RouteNetwork,
        Payer = Payer
    };

    private static MockFacilitatorClient InvalidVerification(string reason) => new()
    {
        VerifyResponse = new VerificationResponse { IsValid = false, InvalidReason = reason, Payer = Payer }
    };

    private static async Task<HttpResponseMessage> PayAsync(
        MockFacilitatorClient facilitator,
        string? handlerCacheControl = null,
        PaymentRequirements? routeRequirements = null,
        Func<HttpContext, Task>? handler = null)
    {
        using var server = CreateTestServer(facilitator, handlerCacheControl: handlerCacheControl, routeRequirements: routeRequirements, handler: handler);
        var client = server.CreateClient();
        client.DefaultRequestHeaders.Add(X402Headers.PaymentSignature, EncodePaymentHeader(CreatePayment(routeRequirements)));
        return await client.GetAsync("/api/premium");
    }

    private static SettlementResponse DecodeSettlement(HttpResponseMessage response)
    {
        Assert.True(response.Headers.TryGetValues(X402Headers.PaymentResponse, out var values));
        var json = Encoding.UTF8.GetString(Convert.FromBase64String(values!.Single()));
        return JsonSerializer.Deserialize<SettlementResponse>(json)!;
    }

    private static PaymentRequirements CreateRequirements(object? extra = null) => new()
    {
        Scheme = "exact",
        Network = RouteNetwork,
        Amount = "10000",
        PayTo = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
        MaxTimeoutSeconds = 60,
        Asset = "0x036CbD53842c5426634e7929541eC2318f3dCF7e",
        Extra = extra
    };

    private static X402Options CreateOptions(object? extra = null, ResourceInfo? resource = null)
    {
        var options = new X402Options { FacilitatorUrl = "https://facilitator.test" };
        options.Routes.Add(new RoutePaymentConfig("/api/premium", CreateRequirements(extra)) { Resource = resource });
        return options;
    }

    private static TestServer CreateTestServer(
        MockFacilitatorClient facilitator,
        ResourceInfo? resource = null,
        string? handlerCacheControl = null,
        PaymentRequirements? routeRequirements = null,
        Func<HttpContext, Task>? handler = null)
    {
        var builder = new WebHostBuilder()
            .ConfigureServices(services => services.AddSingleton<IFacilitatorClient>(facilitator))
            .Configure(app =>
            {
                app.UseX402(options =>
                {
                    options.FacilitatorUrl = "https://facilitator.test";
                    var route = new RoutePaymentConfig("/api/premium", routeRequirements ?? CreateRequirements());
                    route.Resource = resource;
                    options.Routes.Add(route);
                });

                app.Run(context =>
                {
                    if (handler != null)
                        return handler(context);

                    if (handlerCacheControl != null)
                        context.Response.Headers["Cache-Control"] = handlerCacheControl;
                    return context.Response.WriteAsync(PremiumBody);
                });
            });

        return new TestServer(builder);
    }

    private static PaymentPayload CreatePayment(PaymentRequirements? accepted = null) => new()
    {
        Accepted = accepted ?? CreateRequirements(),
        Payload = new ExactSchemePayload
        {
            Signature = "0xsignature",
            Authorization = new Authorization
            {
                From = Payer,
                To = "0x209693Bc6afc0C5328bA36FaF03C514EF312287C",
                Value = "10000",
                ValidAfter = "0",
                ValidBefore = "1740672154",
                Nonce = "0xf3746613c2d920b5fdabc0856f2aeb2d4f88ee6037b8cc5d04a71a4462f13480"
            }
        }
    };

    private static string EncodePaymentHeader(PaymentPayload payload) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload)));
}
