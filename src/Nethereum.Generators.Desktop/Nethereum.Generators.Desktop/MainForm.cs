using System;
using System.Linq;
using Eto.Forms;
using Eto.Drawing;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.Generators.Desktop.Core;

namespace Nethereum.Generators.Desktop
{
	public partial class MainForm : Form
	{
	    public IServiceProvider Services { get; set; }
        public TabPage[] TabPages { get; set;  }

        public MainForm()
		{
		    InitializeServices();
		    InitialiseTabs();
            InitializeComponent();
		}

	    private void InitialiseTabs()
	    {
	        var tabs = Services.GetServices<IMainTab>();
	        TabPages = tabs.OrderBy(x => x.Order).Select(x => x.TabPage).ToArray();
	    }

	    private void InitializeServices()
	    {
	        IServiceCollection serviceCollection = new ServiceCollection();
            Core.Bootstrapper.RegisterServices(serviceCollection);
            Plugin.UnitTestLib.Bootstrapper.RegisterServices(serviceCollection);
            Services = serviceCollection.BuildServiceProvider();
        }
	}
}
