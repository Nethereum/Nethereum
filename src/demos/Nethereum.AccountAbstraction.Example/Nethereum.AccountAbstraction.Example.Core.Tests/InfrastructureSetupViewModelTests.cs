using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Example.Hosting;
using Xunit;

namespace Nethereum.AccountAbstraction.Example.Core.Tests
{
    public class InfrastructureSetupViewModelTests
    {
        private sealed class NeverCalledExternalProvisioner : IExternalInfrastructureProvisioner
        {
            public Task<ProvisionedInfra> ProvisionAsync(ExternalInfrastructureRequest request, Action<string>? log = null) =>
                throw new InvalidOperationException("External provisioning should not run while InfrastructureMode is Embedded.");
        }

        private sealed class NeverCalledExistingProvisioner : IExistingInfrastructureProvisioner
        {
            public Task<ProvisionedInfra> ProvisionAsync(ExistingInfrastructureRequest request, Action<string>? log = null) =>
                throw new InvalidOperationException("Use-existing provisioning should not run while InfrastructureMode is Embedded.");
        }

        private sealed class SpyResource : IAsyncDisposable
        {
            private readonly IAsyncDisposable? _inner;

            public SpyResource(IAsyncDisposable? inner) => _inner = inner;

            public int DisposeCount { get; private set; }

            public async ValueTask DisposeAsync()
            {
                DisposeCount++;
                if (_inner is not null)
                    await _inner.DisposeAsync().ConfigureAwait(false);
            }
        }

        private sealed class SpyingProvisioner : IEmbeddedInfrastructureProvisioner
        {
            private readonly IEmbeddedInfrastructureProvisioner _inner = new EmbeddedInfrastructureProvisioner();

            public List<SpyResource> ProvisionedResources { get; } = new();

            public async Task<ProvisionedInfra> ProvisionAsync(Action<string>? log = null)
            {
                var provisioned = await _inner.ProvisionAsync(log).ConfigureAwait(false);
                var spy = new SpyResource(provisioned.Resource);
                ProvisionedResources.Add(spy);
                return provisioned with { Resource = spy };
            }
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_infrastructure_already_deployed_When_the_user_redeploys_Then_the_previous_infra_is_disposed_and_the_active_account_is_cleared()
        {
            var session = new SessionState();
            var provisioner = new SpyingProvisioner();
            var vm = new InfrastructureSetupViewModel(session, provisioner, new NeverCalledExternalProvisioner(), new NeverCalledExistingProvisioner());

            await vm.DeployCommand.ExecuteAsync(null);
            Assert.Null(vm.ErrorMessage);
            Assert.Single(provisioner.ProvisionedResources);
            var firstResource = provisioner.ProvisionedResources[0];
            Assert.Equal(0, firstResource.DisposeCount);

            var setupViewModel = new SetupViewModel(session);
            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
            Assert.Null(setupViewModel.ErrorMessage);
            Assert.NotNull(session.Account);
            Assert.NotNull(session.AccountDescription);

            try
            {
                await vm.DeployCommand.ExecuteAsync(null);
                Assert.Null(vm.ErrorMessage);

                Assert.Equal(2, provisioner.ProvisionedResources.Count);
                var secondResource = provisioner.ProvisionedResources[1];

                Assert.Equal(1, firstResource.DisposeCount);
                Assert.Equal(0, secondResource.DisposeCount);

                Assert.Null(session.Account);
                Assert.Null(session.AccountDescription);
            }
            finally
            {
                if (session.InfraResource is not null)
                    await session.InfraResource.DisposeAsync();
            }
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_a_fresh_session_When_the_user_deploys_Then_it_becomes_ready_with_every_address_populated()
        {
            var session = new SessionState();
            var vm = new InfrastructureSetupViewModel(session, new EmbeddedInfrastructureProvisioner(), new NeverCalledExternalProvisioner(), new NeverCalledExistingProvisioner());

            Assert.False(session.IsReady);
            Assert.False(vm.IsReady);
            Assert.Equal("Deploy", vm.DeployButtonLabel);

            await vm.DeployCommand.ExecuteAsync(null);

            try
            {
                Assert.Null(vm.ErrorMessage);
                Assert.True(session.IsReady);
                Assert.True(vm.IsReady);
                Assert.Equal("Redeploy", vm.DeployButtonLabel);
                Assert.NotEmpty(vm.Log);

                Assert.False(string.IsNullOrEmpty(vm.EntryPointAddress));
                Assert.False(string.IsNullOrEmpty(vm.FactoryAddress));
                Assert.False(string.IsNullOrEmpty(vm.EcdsaValidatorAddress));
                Assert.False(string.IsNullOrEmpty(vm.WebAuthnValidatorAddress));
                Assert.False(string.IsNullOrEmpty(vm.OwnableExecutorAddress));
                Assert.False(string.IsNullOrEmpty(vm.SocialRecoveryAddress));
                Assert.False(string.IsNullOrEmpty(vm.SmartSessionAddress));
                Assert.False(string.IsNullOrEmpty(vm.PaymasterAddress));

                Assert.NotNull(session.Client);
                Assert.NotNull(session.Bundler);
                Assert.NotNull(session.Web3);
                Assert.NotNull(session.Counter);
                Assert.NotNull(session.Booking);
                Assert.NotNull(session.Paymaster);
                Assert.NotNull(session.Faucet);
                Assert.Equal(vm.EntryPointAddress, session.Addresses!.EntryPointAddress);

                var setupViewModel = new SetupViewModel(session);
                await setupViewModel.CreateAccountCommand.ExecuteAsync(null);
                Assert.Null(setupViewModel.ErrorMessage);
                Assert.NotNull(session.Account);

                await session.Faucet!.FundAsync(session.Account!.Address);

                var interactionViewModel = new InteractionViewModel(session);
                await interactionViewModel.SendCountCommand.ExecuteAsync(null);
                Assert.Null(interactionViewModel.ErrorMessage);
                Assert.NotNull(interactionViewModel.LastReceipt);
                Assert.True(interactionViewModel.LastReceipt!.UserOpSuccess, interactionViewModel.LastReceipt.FailureDiagnostic);
                Assert.Equal(BigInteger.One, interactionViewModel.Count);
            }
            finally
            {
                if (session.InfraResource is not null)
                    await session.InfraResource.DisposeAsync();
            }
        }

        [Fact]
        [Trait("UseCase", "InfrastructureSetup")]
        public async Task Given_infrastructure_never_deployed_When_another_tabs_command_runs_Then_it_fails_with_a_deploy_first_message()
        {
            var session = new SessionState();
            var setupViewModel = new SetupViewModel(session);

            await setupViewModel.CreateAccountCommand.ExecuteAsync(null);

            Assert.False(setupViewModel.IsBusy);
            Assert.Null(session.Account);
            Assert.False(string.IsNullOrEmpty(setupViewModel.ErrorMessage));
            Assert.Contains("Deploy infrastructure", setupViewModel.ErrorMessage);
        }
    }
}
