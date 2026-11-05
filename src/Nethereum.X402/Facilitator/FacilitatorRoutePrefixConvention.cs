using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ApplicationModels;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Nethereum.X402.Facilitator;

internal sealed class FacilitatorRoutePrefixConvention : IApplicationModelConvention
{
    private readonly Func<string> _prefix;

    public FacilitatorRoutePrefixConvention(Func<string> prefix)
    {
        _prefix = prefix;
    }

    public void Apply(ApplicationModel application)
    {
        var prefix = (_prefix() ?? X402FacilitatorOptions.DefaultRoutePrefix).Trim('/');
        var controllerType = typeof(FacilitatorController).GetTypeInfo();

        foreach (var controller in application.Controllers.Where(candidate => candidate.ControllerType == controllerType))
        {
            foreach (var selector in controller.Selectors.Where(candidate => candidate.AttributeRouteModel != null))
                selector.AttributeRouteModel!.Template = prefix;
        }
    }
}

internal sealed class FacilitatorRoutePrefixSetup : IConfigureOptions<MvcOptions>
{
    private readonly IServiceProvider _services;

    public FacilitatorRoutePrefixSetup(IServiceProvider services)
    {
        _services = services;
    }

    public void Configure(MvcOptions options) =>
        options.Conventions.Add(new FacilitatorRoutePrefixConvention(
            () => _services.GetService<X402FacilitatorOptions>()?.RoutePrefix ?? X402FacilitatorOptions.DefaultRoutePrefix));
}
