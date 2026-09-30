using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public MainWindow(IServiceProvider services) : this()
    {
        var session = services.GetRequiredService<SessionState>();
        DataContext = session;

        SetupTab.DataContext = services.GetRequiredService<InfrastructureSetupViewModel>();
        AdminTab.DataContext = services.GetRequiredService<EnterpriseAdminViewModel>();
        OperatorTab.DataContext = services.GetRequiredService<EnterpriseOperatorViewModel>();
        TieredApprovalTab.DataContext = services.GetRequiredService<TieredApprovalViewModel>();
        OffboardTab.DataContext = services.GetRequiredService<OffboardViewModel>();
    }
}
