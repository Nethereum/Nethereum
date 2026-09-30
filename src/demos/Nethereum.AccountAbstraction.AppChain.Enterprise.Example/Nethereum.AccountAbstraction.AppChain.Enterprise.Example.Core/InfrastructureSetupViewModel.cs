using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core
{
    public enum InfrastructureMode
    {
        Embedded,
        External
    }

    public partial class InfrastructureSetupViewModel : TabViewModel
    {
        private readonly SessionState _session;
        private readonly IEmbeddedInfrastructureProvisioner _embeddedProvisioner;
        private readonly IExternalInfrastructureProvisioner _externalProvisioner;

        [ObservableProperty]
        private InfrastructureMode _mode = InfrastructureMode.Embedded;

        [ObservableProperty]
        private string? _nodeRpcUrl;

        [ObservableProperty]
        private string? _bundlerUrl;

        [ObservableProperty]
        private string? _funderPrivateKey;

        [ObservableProperty]
        private string? _entryPointAddress;

        [ObservableProperty]
        private string? _accountFactoryAddress;

        [ObservableProperty]
        private string? _accountRegistryAddress;

        [ObservableProperty]
        private string? _sponsoredPaymasterAddress;

        [ObservableProperty]
        private string? _valueCapCombinatorAddress;

        [ObservableProperty]
        private string? _payableTargetAddress;

        public IReadOnlyList<InfrastructureMode> AvailableModes { get; } = new[] { InfrastructureMode.Embedded, InfrastructureMode.External };

        public ObservableCollection<string> Log { get; } = new();

        public bool IsReady => _session.IsReady;

        public string DeployButtonLabel => IsReady ? "Redeploy" : "Deploy";

        public InfrastructureSetupViewModel(
            SessionState session,
            IEmbeddedInfrastructureProvisioner embeddedProvisioner,
            IExternalInfrastructureProvisioner externalProvisioner)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _embeddedProvisioner = embeddedProvisioner ?? throw new ArgumentNullException(nameof(embeddedProvisioner));
            _externalProvisioner = externalProvisioner ?? throw new ArgumentNullException(nameof(externalProvisioner));
            _session.InfraChanged += () =>
            {
                OnPropertyChanged(nameof(IsReady));
                OnPropertyChanged(nameof(DeployButtonLabel));
            };
        }

        [RelayCommand]
        private Task DeployAsync() => RunAsync(async () =>
        {
            Log.Clear();
            var provisioned = await ProvisionSelectedModeAsync().ConfigureAwait(false);

            var previousResource = _session.PublishInfra(provisioned);
            if (previousResource is not null)
                await previousResource.DisposeAsync().ConfigureAwait(false);

            EntryPointAddress = provisioned.Deployment.EntryPointAddress;
            AccountFactoryAddress = provisioned.Deployment.AccountFactoryAddress;
            AccountRegistryAddress = provisioned.Deployment.AccountRegistryAddress;
            SponsoredPaymasterAddress = provisioned.Deployment.SponsoredPaymasterAddress;
            ValueCapCombinatorAddress = provisioned.ValueCapCombinatorAddress;
            PayableTargetAddress = provisioned.PayableTargetAddress;

            StatusMessage = Mode == InfrastructureMode.External
                ? $"Connected to your node/bundler and deployed the AppChain AA stack + P1-D rule stack through your funder account (EntryPoint {EntryPointAddress}). Admin/Operator are now unlocked."
                : "Infrastructure deployed - EntryPoint, AccountRegistry, factory, SponsoredPaymaster and the P1-D rule stack are live. Admin/Operator are now unlocked.";
        });

        private Task<ProvisionedEnterpriseInfra> ProvisionSelectedModeAsync() => Mode switch
        {
            InfrastructureMode.Embedded => _embeddedProvisioner.ProvisionAsync(line => Log.Add(line)),
            InfrastructureMode.External => _externalProvisioner.ProvisionAsync(BuildExternalRequest(), line => Log.Add(line)),
            _ => throw new NotSupportedException($"Infrastructure mode '{Mode}' is not wired up yet.")
        };

        private ExternalInfrastructureRequest BuildExternalRequest()
        {
            if (string.IsNullOrWhiteSpace(NodeRpcUrl) || !Uri.TryCreate(NodeRpcUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a valid node RPC url (e.g. http://localhost:8545).");
            if (string.IsNullOrWhiteSpace(BundlerUrl) || !Uri.TryCreate(BundlerUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a valid bundler url.");
            if (string.IsNullOrWhiteSpace(FunderPrivateKey))
                throw new InvalidOperationException("Enter the funder account's private key - it pays for every AppChain contract deployment.");

            return new ExternalInfrastructureRequest(NodeRpcUrl.Trim(), BundlerUrl.Trim(), FunderPrivateKey.Trim());
        }
    }
}
