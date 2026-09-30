using System;
using Avalonia.Controls;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Example.Core;

namespace Nethereum.AccountAbstraction.Example.Avalonia;

public partial class MainWindow : Window
{
    public MainWindow() => InitializeComponent();

    public MainWindow(IServiceProvider services) : this()
    {
        var session = services.GetRequiredService<SessionState>();
        DataContext = session;

        AccountBanner.DataContext = services.GetRequiredService<AccountBannerViewModel>();

        InfraTab.DataContext = services.GetRequiredService<InfrastructureSetupViewModel>();

        SetupTab.DataContext = services.GetRequiredService<SetupViewModel>();

        SendTab.DataContext = services.GetRequiredService<InteractionViewModel>();
        BatchTab.DataContext = services.GetRequiredService<BatchViewModel>();
        GaslessTab.DataContext = services.GetRequiredService<GaslessViewModel>();
        CustomTab.DataContext = services.GetRequiredService<CustomViewModel>();
        PasskeyTab.DataContext = services.GetRequiredService<PasskeyAccountViewModel>();
        Eip7702Tab.DataContext = services.GetRequiredService<Eip7702AccountViewModel>();
        ModulesTab.DataContext = services.GetRequiredService<ModulesViewModel>();
        PoliciesTab.DataContext = services.GetRequiredService<PoliciesViewModel>();
        WorkflowsTab.DataContext = services.GetRequiredService<WorkflowsViewModel>();
        SocialRecoveryTab.DataContext = services.GetRequiredService<SocialRecoveryViewModel>();
        DiagnosticsTab.DataContext = services.GetRequiredService<DiagnosticsViewModel>();
    }
}
