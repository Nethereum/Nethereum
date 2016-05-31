using Microsoft.Owin;
using Owin;

[assembly: OwinStartupAttribute(typeof(Nethereum.Web.Sample.Startup))]
namespace Nethereum.Web.Sample
{
    public partial class Startup
    {
        public void Configuration(IAppBuilder app)
        {
            ConfigureAuth(app);
            AutomapperWebConfiguration.Configure();
        }
    }
}
