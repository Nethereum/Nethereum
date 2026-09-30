using System;
using System.Collections.Generic;
using CommunityToolkit.Mvvm.ComponentModel;
using Nethereum.AccountAbstraction;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Modules.Rhinestone.OwnableExecutor;
using Nethereum.AccountAbstraction.Contracts.Modules.SmartSessions.SmartSession;
using Nethereum.AccountAbstraction.Example.Contracts.BookingRegistry;
using Nethereum.AccountAbstraction.Example.Contracts.TestCounter;
using Nethereum.RPC;
using Nethereum.Web3;

namespace Nethereum.AccountAbstraction.Example.Core
{
    public partial class SessionState : ObservableObject
    {
        public IAAClient? Client { get; set; }

        public IAccountAbstractionBundlerService? Bundler { get; set; }

        public IWeb3? Web3 { get; set; }

        public AADeploymentAddresses? Addresses { get; set; }
        public TestCounterService? Counter { get; set; }
        public BookingRegistryService? Booking { get; set; }
        public PaymasterConfig? Paymaster { get; set; }
        public WebAuthnAccountConfig? WebAuthnConfig { get; set; }
        public Eip7702AccountConfig? Eip7702Config { get; set; }
        public SocialRecoveryAccountConfig? SocialRecoveryConfig { get; set; }
        public PoliciesAccountConfig? PoliciesConfig { get; set; }
        public OwnableExecutorService? OwnableExecutor { get; set; }
        public SmartSessionService? SmartSession { get; set; }
        public IDevChainFaucet? Faucet { get; set; }

        public bool HasPaymaster => Paymaster is not null;

        public IReadOnlyList<AccountPaymentMode> AvailablePaymentModes => HasPaymaster
            ? new[] { AccountPaymentMode.SelfFunded, AccountPaymentMode.PaymasterSponsored }
            : new[] { AccountPaymentMode.SelfFunded };

        public IAsyncDisposable? InfraResource { get; set; }

        [ObservableProperty]
        private bool _isReady;

        public event Action? InfraChanged;

        [ObservableProperty]
        private NethereumSmartAccount? _account;

        [ObservableProperty]
        private string? _accountDescription;

        [ObservableProperty]
        private AccountPaymentMode _paymentMode = AccountPaymentMode.SelfFunded;

        public event Action? BalanceMayHaveChanged;

        public IAsyncDisposable? PublishInfra(DeployedStack stack, IWeb3 web3, IAccountAbstractionBundlerService bundler, IDevChainFaucet faucet, IAsyncDisposable? resource = null)
        {
            if (stack is null) throw new ArgumentNullException(nameof(stack));
            var previousResource = InfraResource;
            Client = stack.Client;
            Addresses = stack.Addresses;
            Counter = stack.TestCounter;
            Booking = stack.BookingRegistry;
            Paymaster = stack.Paymaster;
            WebAuthnConfig = stack.WebAuthnConfig;
            Eip7702Config = stack.Eip7702Config;
            SocialRecoveryConfig = stack.SocialRecoveryConfig;
            PoliciesConfig = stack.PoliciesConfig;
            OwnableExecutor = stack.OwnableExecutor;
            SmartSession = stack.SmartSession;
            Web3 = web3 ?? throw new ArgumentNullException(nameof(web3));
            Bundler = bundler ?? throw new ArgumentNullException(nameof(bundler));
            Faucet = faucet ?? throw new ArgumentNullException(nameof(faucet));
            InfraResource = resource;
            Account = null;
            AccountDescription = null;
            PaymentMode = AccountPaymentMode.SelfFunded;
            IsReady = true;
            InfraChanged?.Invoke();
            return previousResource;
        }

        public void RequireReady()
        {
            if (!IsReady)
                throw new InvalidOperationException("Deploy infrastructure in Setup first.");
        }

        public void RaiseAccountChanged(NethereumSmartAccount account, string description)
        {
            Account = account ?? throw new ArgumentNullException(nameof(account));
            AccountDescription = string.IsNullOrWhiteSpace(description)
                ? throw new ArgumentException("A description of the account's signer is required.", nameof(description))
                : description;
            PaymentMode = AccountPaymentMode.SelfFunded;
        }

        public void NotifyBalanceChanged() => BalanceMayHaveChanged?.Invoke();

        public void ApplyPaymentMode(AAContractHandler handler)
        {
            if (PaymentMode == AccountPaymentMode.PaymasterSponsored && Paymaster is not null)
                handler.WithPaymaster(Paymaster);
        }
    }
}
