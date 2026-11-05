using System.Net;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nethereum.X402.Extensions;
using Nethereum.X402.Models;
using Nethereum.X402.Processors;

namespace Nethereum.X402.IntegrationTests.Facilitator;

public class X402FacilitatorAuthorizationTests
{
    private const string SchemeName = "HeaderAuth";
    private const string Policy = "facilitator-callers";
    private const string UserHeader = "X-Test-User";
    private const string RoleHeader = "X-Test-Role";

    [Theory]
    [Trait("Rule", "V2")]
    [InlineData("GET", "/facilitator/supported")]
    [InlineData("POST", "/facilitator/verify")]
    [InlineData("POST", "/facilitator/settle")]
    public async Task Given_AnAuthorizationPolicy_When_AnUnauthenticatedCallerUsesAFacilitatorEndpoint_Then_ItIsRejectedWith401(string method, string path)
    {
        using var server = CreateServer(Policy);

        var response = await server.CreateClient().SendAsync(Request(method, path, null, null));

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AnAuthorizationPolicy_When_AnAuthenticatedCallerThatSatisfiesItUsesTheEndpoint_Then_TheCallSucceeds()
    {
        using var server = CreateServer(Policy);

        var response = await server.CreateClient().SendAsync(Request("GET", "/facilitator/supported", "resource-server", "settler"));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_AnAuthorizationPolicy_When_AnAuthenticatedCallerThatFailsItUsesTheEndpoint_Then_ItIsForbidden()
    {
        using var server = CreateServer(Policy);

        var response = await server.CreateClient().SendAsync(Request("GET", "/facilitator/supported", "resource-server", "other"));

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    [Trait("Rule", "V2")]
    public async Task Given_NoAuthorizationPolicyConfigured_When_AnUnauthenticatedCallerUsesTheEndpoint_Then_TheCallSucceeds()
    {
        using var server = CreateServer(null);

        var response = await server.CreateClient().SendAsync(Request("GET", "/facilitator/supported", null, null));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    private static HttpRequestMessage Request(string method, string path, string? user, string? role)
    {
        var request = new HttpRequestMessage(new HttpMethod(method), path);
        if (method == "POST")
            request.Content = new StringContent("{}", Encoding.UTF8, "application/json");
        if (user != null)
            request.Headers.Add(UserHeader, user);
        if (role != null)
            request.Headers.Add(RoleHeader, role);
        return request;
    }

    private static TestServer CreateServer(string? policy) => new(new WebHostBuilder()
        .ConfigureServices(services =>
        {
            services.AddSingleton<IX402PaymentProcessor>(new SupportedOnlyProcessor());
            services.AddX402FacilitatorOptions(options => options.AuthorizationPolicy = policy);
            services.AddAuthentication(SchemeName).AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(SchemeName, _ => { });
            services.AddAuthorization(options => options.AddPolicy(Policy, builder =>
                builder.AddAuthenticationSchemes(SchemeName).RequireAuthenticatedUser().RequireRole("settler")));
            services.AddControllers().AddX402FacilitatorControllers();
        })
        .Configure(app =>
        {
            app.UseRouting();
            app.UseAuthentication();
            app.UseAuthorization();
            app.UseEndpoints(endpoints => endpoints.MapControllers());
        }));

    private sealed class HeaderAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        public HeaderAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder)
            : base(options, logger, encoder)
        {
        }

        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue(UserHeader, out var user))
                return Task.FromResult(AuthenticateResult.NoResult());

            var claims = new List<Claim> { new(ClaimTypes.Name, user.ToString()) };
            if (Request.Headers.TryGetValue(RoleHeader, out var role))
                claims.Add(new Claim(ClaimTypes.Role, role.ToString()));

            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, SchemeName));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, SchemeName)));
        }
    }

    private sealed class SupportedOnlyProcessor : IX402PaymentProcessor
    {
        public Task<VerificationResponse> VerifyPaymentAsync(
            PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            Task.FromResult(new VerificationResponse { IsValid = true });

        public Task<SettlementResponse> SettlePaymentAsync(
            PaymentPayload paymentPayload, PaymentRequirements requirements, CancellationToken cancellationToken = default) =>
            Task.FromResult(new SettlementResponse { Success = true });

        public Task<SupportedPaymentKindsResponse> GetSupportedAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new SupportedPaymentKindsResponse { Kinds = new List<PaymentKind>() });
    }
}
