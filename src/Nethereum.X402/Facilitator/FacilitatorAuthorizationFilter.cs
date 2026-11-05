using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;

namespace Nethereum.X402.Facilitator;

internal sealed class FacilitatorAuthorizationFilter : IAsyncAuthorizationFilter
{
    private readonly X402FacilitatorOptions? _options;

    public FacilitatorAuthorizationFilter(IServiceProvider services)
    {
        _options = services.GetService<X402FacilitatorOptions>();
    }

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        var policyName = _options?.AuthorizationPolicy;
        if (string.IsNullOrEmpty(policyName))
            return;

        var services = context.HttpContext.RequestServices;
        var policy = await services.GetRequiredService<IAuthorizationPolicyProvider>().GetPolicyAsync(policyName)
            ?? throw new InvalidOperationException($"The authorization policy '{policyName}' configured for the x402 facilitator is not registered.");

        var evaluator = services.GetRequiredService<IPolicyEvaluator>();
        var authentication = await evaluator.AuthenticateAsync(policy, context.HttpContext);
        var authorization = await evaluator.AuthorizeAsync(policy, authentication, context.HttpContext, context);

        if (authorization.Challenged)
            context.Result = new ChallengeResult(policy.AuthenticationSchemes.ToArray());
        else if (authorization.Forbidden)
            context.Result = new ForbidResult(policy.AuthenticationSchemes.ToArray());
    }
}
