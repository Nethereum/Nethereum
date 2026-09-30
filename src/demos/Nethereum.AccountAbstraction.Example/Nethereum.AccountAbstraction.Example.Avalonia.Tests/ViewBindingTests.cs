using System.Linq;
using Avalonia.Controls;
using Avalonia.Headless.XUnit;
using Avalonia.VisualTree;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.Example.Avalonia.Views;
using Nethereum.AccountAbstraction.Example.Core;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Avalonia.Tests;

[Collection(AvaloniaHostCollection.COLLECTION_NAME)]
public class ViewBindingTests
{
    private readonly AvaloniaHostFixture _fixture;

    public ViewBindingTests(AvaloniaHostFixture fixture)
    {
        _fixture = fixture;
    }

    private static Window ShowInWindow(Control content)
    {
        var window = new Window { Content = content };
        window.Show();
        return window;
    }

    [AvaloniaFact]
    public void SetupView_renders_and_its_button_is_bound_to_CreateAccountCommand()
    {
        var vm = _fixture.Services.GetRequiredService<SetupViewModel>();
        var view = new SetupView { DataContext = vm };
        var window = ShowInWindow(view);

        Assert.True(view.IsEffectivelyVisible);
        var createButton = view.GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(b.Content, "Create Account"));
        Assert.Same(vm.CreateAccountCommand, createButton.Command);

        window.Close();
    }

    [AvaloniaFact]
    public void SendView_renders_and_its_buttons_are_bound_to_the_interaction_commands()
    {
        var vm = _fixture.Services.GetRequiredService<InteractionViewModel>();
        var view = new SendView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Send Count") && ReferenceEquals(b.Command, vm.SendCountCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Send Failing Count") && ReferenceEquals(b.Command, vm.SendFailingCountCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void BatchView_renders_and_its_buttons_are_bound_to_the_batch_commands()
    {
        var vm = _fixture.Services.GetRequiredService<BatchViewModel>();
        var view = new BatchView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Batch: 2x Count") && ReferenceEquals(b.Command, vm.SendBatchCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Batch: Count + Failing Call") && ReferenceEquals(b.Command, vm.SendBatchWithAFailingCallCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void GaslessView_renders_and_its_button_is_bound_to_SendGaslessCommand()
    {
        var vm = _fixture.Services.GetRequiredService<GaslessViewModel>();
        var view = new GaslessView { DataContext = vm };
        var window = ShowInWindow(view);

        var button = view.GetVisualDescendants().OfType<Button>().Single();
        Assert.Same(vm.SendGaslessCommand, button.Command);

        window.Close();
    }

    [AvaloniaFact]
    public void CustomView_renders_and_its_buttons_are_bound_to_the_registry_commands()
    {
        var vm = _fixture.Services.GetRequiredService<CustomViewModel>();
        var view = new CustomView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Book Slot") && ReferenceEquals(b.Command, vm.BookCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Guest Of Slot") && ReferenceEquals(b.Command, vm.GuestOfCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Release Slot") && ReferenceEquals(b.Command, vm.ReleaseCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void PasskeyView_renders_and_its_buttons_are_bound_to_the_passkey_commands()
    {
        var vm = _fixture.Services.GetRequiredService<PasskeyAccountViewModel>();
        var view = new PasskeyView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Create Passkey Account") && ReferenceEquals(b.Command, vm.CreateAccountCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Send Count (passkey-signed)") && ReferenceEquals(b.Command, vm.SendCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void Eip7702View_renders_and_its_buttons_are_bound_to_the_eip7702_commands()
    {
        var vm = _fixture.Services.GetRequiredService<Eip7702AccountViewModel>();
        var view = new Eip7702View { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Generate EOA") && ReferenceEquals(b.Command, vm.GenerateEoaCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Fund EOA") && ReferenceEquals(b.Command, vm.FundEoaCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Upgrade via EIP-7702 & Send") && ReferenceEquals(b.Command, vm.UpgradeAndSendCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void ModulesView_renders_and_its_buttons_are_bound_to_the_modules_commands()
    {
        var vm = _fixture.Services.GetRequiredService<ModulesViewModel>();
        var view = new ModulesView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Install passkey as second validator") && ReferenceEquals(b.Command, vm.InstallPasskeyValidatorCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void PoliciesView_renders_and_its_buttons_are_bound_to_the_policies_commands()
    {
        var vm = _fixture.Services.GetRequiredService<PoliciesViewModel>();
        var view = new PoliciesView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Create scoped session key") && ReferenceEquals(b.Command, vm.CreateScopedSessionKeyCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void WorkflowsView_renders_and_its_buttons_are_bound_to_the_workflows_commands()
    {
        var vm = _fixture.Services.GetRequiredService<WorkflowsViewModel>();
        var view = new WorkflowsView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Create automation agent") && ReferenceEquals(b.Command, vm.CreateAgentCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void SocialRecoveryView_renders_and_its_button_is_bound_to_SetupGuardiansCommand()
    {
        var vm = _fixture.Services.GetRequiredService<SocialRecoveryViewModel>();
        var view = new SocialRecoveryView { DataContext = vm };
        var window = ShowInWindow(view);

        var button = view.GetVisualDescendants().OfType<Button>()
            .Single(b => Equals(b.Content, "Set up guardians"));
        Assert.Same(vm.SetupGuardiansCommand, button.Command);

        window.Close();
    }

    [AvaloniaFact]
    public void DiagnosticsView_renders_and_its_buttons_are_bound_to_the_diagnostics_commands()
    {
        var vm = _fixture.Services.GetRequiredService<DiagnosticsViewModel>();
        var view = new DiagnosticsView { DataContext = vm };
        var window = ShowInWindow(view);

        var buttons = view.GetVisualDescendants().OfType<Button>().ToList();
        Assert.Contains(buttons, b => Equals(b.Content, "Run diagnosed op (success)") && ReferenceEquals(b.Command, vm.RunDiagnosedOpCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Run reverting op") && ReferenceEquals(b.Command, vm.RunRevertingOpCommand));
        Assert.Contains(buttons, b => Equals(b.Content, "Run out-of-gas op") && ReferenceEquals(b.Command, vm.RunOutOfGasOpCommand));

        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_hosts_all_thirteen_tabs_wired_to_resolved_view_models()
    {
        var window = new MainWindow(_fixture.Services);
        window.Show();

        var tabControl = window.GetVisualDescendants().OfType<TabControl>().Single();
        Assert.Equal(13, tabControl.Items.Count);

        var session = _fixture.Services.GetRequiredService<SessionState>();
        Assert.Same(session, window.DataContext);

        window.Close();
    }

    [AvaloniaFact]
    public void MainWindow_account_banner_binds_to_the_resolved_AccountBannerViewModel()
    {
        var window = new MainWindow(_fixture.Services);
        window.Show();

        var banner = _fixture.Services.GetRequiredService<AccountBannerViewModel>();
        var bannerText = window.GetVisualDescendants().OfType<TextBlock>()
            .Single(t => ReferenceEquals(t.DataContext, banner) && t.IsEffectivelyVisible);

        Assert.Equal(banner.DisplayText, bannerText.Text);

        window.Close();
    }
}
