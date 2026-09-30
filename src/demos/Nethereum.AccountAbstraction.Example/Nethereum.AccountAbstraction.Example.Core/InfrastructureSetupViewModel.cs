using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public enum InfrastructureMode
    {
        Embedded,
        External,
        UseExisting
    }

    public partial class InfrastructureSetupViewModel : TabViewModel
    {
        private readonly SessionState _session;
        private readonly IEmbeddedInfrastructureProvisioner _embeddedProvisioner;
        private readonly IExternalInfrastructureProvisioner _externalProvisioner;
        private readonly IExistingInfrastructureProvisioner _existingProvisioner;

        [ObservableProperty]
        private InfrastructureMode _mode = InfrastructureMode.Embedded;

        [ObservableProperty]
        private string? _nodeRpcUrl;

        [ObservableProperty]
        private string? _bundlerUrl;

        [ObservableProperty]
        private string? _funderPrivateKey;

        [ObservableProperty]
        private string? _existingEntryPointAddress;

        [ObservableProperty]
        private string? _existingFactoryAddress;

        [ObservableProperty]
        private string? _existingEcdsaValidatorAddress;

        [ObservableProperty]
        private string? _existingWebAuthnValidatorAddress;

        [ObservableProperty]
        private string? _existingOwnableExecutorAddress;

        [ObservableProperty]
        private string? _existingSocialRecoveryAddress;

        [ObservableProperty]
        private string? _existingSmartSessionAddress;

        [ObservableProperty]
        private string? _existingSudoPolicyAddress;

        [ObservableProperty]
        private string? _existingUniActionPolicyAddress;

        [ObservableProperty]
        private string? _existingEcdsaSessionValidatorAddress;

        [ObservableProperty]
        private string? _existingAccountImplementationAddress;

        [ObservableProperty]
        private string? _entryPointAddress;

        [ObservableProperty]
        private string? _factoryAddress;

        [ObservableProperty]
        private string? _ecdsaValidatorAddress;

        [ObservableProperty]
        private string? _webAuthnValidatorAddress;

        [ObservableProperty]
        private string? _ownableExecutorAddress;

        [ObservableProperty]
        private string? _socialRecoveryAddress;

        [ObservableProperty]
        private string? _smartSessionAddress;

        [ObservableProperty]
        private string? _paymasterAddress;

        public IReadOnlyList<InfrastructureMode> AvailableModes { get; } = new[] { InfrastructureMode.Embedded, InfrastructureMode.External, InfrastructureMode.UseExisting };

        public ObservableCollection<string> Log { get; } = new();

        public bool IsReady => _session.IsReady;

        public string DeployButtonLabel => IsReady ? "Redeploy" : "Deploy";

        public InfrastructureSetupViewModel(
            SessionState session,
            IEmbeddedInfrastructureProvisioner embeddedProvisioner,
            IExternalInfrastructureProvisioner externalProvisioner,
            IExistingInfrastructureProvisioner existingProvisioner)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _embeddedProvisioner = embeddedProvisioner ?? throw new ArgumentNullException(nameof(embeddedProvisioner));
            _externalProvisioner = externalProvisioner ?? throw new ArgumentNullException(nameof(externalProvisioner));
            _existingProvisioner = existingProvisioner ?? throw new ArgumentNullException(nameof(existingProvisioner));
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

            var previousResource = _session.PublishInfra(provisioned.Stack, provisioned.Web3, provisioned.Bundler, provisioned.Faucet, provisioned.Resource);
            if (previousResource is not null)
                await previousResource.DisposeAsync().ConfigureAwait(false);

            var stack = provisioned.Stack;
            EntryPointAddress = stack.Addresses.EntryPointAddress;
            FactoryAddress = stack.Addresses.NethereumAccountFactoryAddress;
            EcdsaValidatorAddress = stack.Addresses.EcdsaValidatorAddress;
            WebAuthnValidatorAddress = stack.WebAuthnConfig.ValidatorAddress;
            OwnableExecutorAddress = stack.OwnableExecutor.ContractAddress;
            SocialRecoveryAddress = stack.SocialRecoveryConfig.SocialRecoveryAddress;
            SmartSessionAddress = stack.PoliciesConfig.SmartSessionAddress;
            PaymasterAddress = stack.Paymaster?.Address;

            StatusMessage = Mode switch
            {
                InfrastructureMode.UseExisting =>
                    "Connected to the provider's existing infrastructure - the standard modules above are theirs, unchanged. Only the demo's own target contracts (TestCounter, BookingRegistry) were deployed, through your funder account. No paymaster is configured in this mode, so Gasless is unavailable.",
                _ when stack.EntryPointWasFreshlyDeployed =>
                    $"Infrastructure deployed, but your bundler's EntryPoint was not present on the node, so a NEW EntryPoint was deployed at {EntryPointAddress}. UserOperations will only work if your bundler is configured to use THIS EntryPoint address - otherwise reconfigure your bundler (or pre-deploy the canonical EntryPoint) and redeploy.",
                _ => "Infrastructure deployed - EntryPoint, factory, validators, modules and demo contracts are live. Every other tab is now unlocked."
            };
        });

        private Task<ProvisionedInfra> ProvisionSelectedModeAsync() => Mode switch
        {
            InfrastructureMode.Embedded => _embeddedProvisioner.ProvisionAsync(line => Log.Add(line)),
            InfrastructureMode.External => _externalProvisioner.ProvisionAsync(BuildExternalRequest(), line => Log.Add(line)),
            InfrastructureMode.UseExisting => _existingProvisioner.ProvisionAsync(BuildExistingRequest(), line => Log.Add(line)),
            _ => throw new NotSupportedException($"Infrastructure mode '{Mode}' is not wired up yet.")
        };

        private ExternalInfrastructureRequest BuildExternalRequest()
        {
            if (string.IsNullOrWhiteSpace(NodeRpcUrl) || !Uri.TryCreate(NodeRpcUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a valid node RPC url (e.g. http://localhost:8545).");
            if (string.IsNullOrWhiteSpace(BundlerUrl) || !Uri.TryCreate(BundlerUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a valid bundler url.");
            if (string.IsNullOrWhiteSpace(FunderPrivateKey))
                throw new InvalidOperationException("Enter the funder account's private key - it pays for every contract deployment.");

            return new ExternalInfrastructureRequest(NodeRpcUrl.Trim(), BundlerUrl.Trim(), FunderPrivateKey.Trim());
        }

        private ExistingInfrastructureRequest BuildExistingRequest()
        {
            if (string.IsNullOrWhiteSpace(NodeRpcUrl) || !Uri.TryCreate(NodeRpcUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a valid node RPC url (e.g. http://localhost:8545).");
            if (string.IsNullOrWhiteSpace(BundlerUrl) || !Uri.TryCreate(BundlerUrl, UriKind.Absolute, out _))
                throw new InvalidOperationException("Enter a valid bundler url.");
            if (string.IsNullOrWhiteSpace(FunderPrivateKey))
                throw new InvalidOperationException("Enter the funder account's private key - it pays for the demo's own target contracts (TestCounter, BookingRegistry).");

            var modules = new ExistingModuleAddresses(
                RequireExistingAddress(ExistingEntryPointAddress, "EntryPoint"),
                RequireExistingAddress(ExistingFactoryAddress, "NethereumAccountFactory"),
                RequireExistingAddress(ExistingEcdsaValidatorAddress, "ECDSAValidator"),
                RequireExistingAddress(ExistingWebAuthnValidatorAddress, "WebAuthnValidator"),
                RequireExistingAddress(ExistingOwnableExecutorAddress, "OwnableExecutor"),
                RequireExistingAddress(ExistingSocialRecoveryAddress, "SocialRecovery"),
                RequireExistingAddress(ExistingSmartSessionAddress, "SmartSession"),
                RequireExistingAddress(ExistingSudoPolicyAddress, "SudoPolicy"),
                RequireExistingAddress(ExistingUniActionPolicyAddress, "UniActionPolicy"),
                RequireExistingAddress(ExistingEcdsaSessionValidatorAddress, "ECDSASessionValidator"),
                RequireExistingAddress(ExistingAccountImplementationAddress, "NethereumAccount implementation"));

            return new ExistingInfrastructureRequest(NodeRpcUrl.Trim(), BundlerUrl.Trim(), FunderPrivateKey.Trim(), modules);
        }

        private static string RequireExistingAddress(string? address, string moduleName) =>
            string.IsNullOrWhiteSpace(address)
                ? throw new InvalidOperationException($"Enter the provider's already-deployed {moduleName} address.")
                : address.Trim();
    }
}
