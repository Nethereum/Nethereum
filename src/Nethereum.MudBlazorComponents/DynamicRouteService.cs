using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Routing;
using MudBlazor;
using System.Reflection;

namespace Nethereum.MudBlazorComponents
{
    public class DynamicRouteService
    {
        //.gen is converted to _gen as the type name
        public List<NavItem> GetGeneratedRoutes(string suffixFilter = "_gen")
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a =>
                {
                    try { return a.GetExportedTypes(); } catch { return Enumerable.Empty<Type>(); }
                })
               .Where(t => typeof(ComponentBase).IsAssignableFrom(t))
               .Where(t => {
                       // Console.WriteLine(t.Name);
                        return t.Name.EndsWith(suffixFilter, StringComparison.OrdinalIgnoreCase);
                    })
                .SelectMany(t => t.GetCustomAttributes<RouteAttribute>().Select(r => new { t.Name, r.Template }))
                .Where(x => !string.IsNullOrWhiteSpace(x.Template))
                .Select(r => new NavItem
                {
                    Title = r.Name.Replace(suffixFilter, "", StringComparison.OrdinalIgnoreCase),
                    Href = r.Template,
                    Icon = Icons.Material.Filled.List
                })
               .ToList();
        }
    }

    public class NavItem
    {
        public string Title { get; set; }
        public string Href { get; set; }
        public string Icon { get; set; }
        public NavLinkMatch Match { get; set; } = NavLinkMatch.Prefix;
    }
}
