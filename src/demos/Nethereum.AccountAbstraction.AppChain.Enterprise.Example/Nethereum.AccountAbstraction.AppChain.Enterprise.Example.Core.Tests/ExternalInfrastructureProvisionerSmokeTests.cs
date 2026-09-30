using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core;
using Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Hosting;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.Enterprise.Example.Core.Tests
{
    public class ExternalInfrastructureProvisionerSmokeTests
    {
        private static string? NodeUrl => Environment.GetEnvironmentVariable("AA_ENTERPRISE_EXTERNAL_NODE_URL");
        private static string? BundlerUrl => Environment.GetEnvironmentVariable("AA_ENTERPRISE_EXTERNAL_BUNDLER_URL");
        private static string? FunderKey => Environment.GetEnvironmentVariable("AA_ENTERPRISE_EXTERNAL_FUNDER_KEY");

        private static bool IsConfigured =>
            !string.IsNullOrWhiteSpace(NodeUrl) && !string.IsNullOrWhiteSpace(BundlerUrl) && !string.IsNullOrWhiteSpace(FunderKey);

        [Fact]
        [Trait("UseCase", "ExternalInfrastructure")]
        public async Task Given_a_running_external_node_and_bundler_When_provisioning_externally_Then_enroll_install_and_pay_within_cap_succeed_over_http()
        {
            if (!IsConfigured)
            {
                return;
            }

            var provisioner = new ExternalEnterpriseInfrastructureProvisioner();
            var request = new ExternalInfrastructureRequest(NodeUrl!, BundlerUrl!, FunderKey!);
            var log = new List<string>();

            var infra = await provisioner.ProvisionAsync(request, line => log.Add(line));

            Assert.NotNull(infra.Deployment);
            Assert.False(string.IsNullOrEmpty(infra.Deployment.EntryPointAddress));
            Assert.Null(infra.Resource);

            var session = new SessionState();
            session.PublishInfra(infra);
            Assert.True(session.IsReady);

            var adminVm = new EnterpriseAdminViewModel(session) { UserId = "external-smoke-user" };
            await adminVm.EnrollCommand.ExecuteAsync(null);
            Assert.Null(adminVm.ErrorMessage);
            Assert.False(string.IsNullOrEmpty(adminVm.ResolvedAddress));

            await infra.Web3.Eth.GetEtherTransferService()
                .TransferEtherAndWaitForReceiptAsync(adminVm.ResolvedAddress!, 10m);

            var operatorVm = new EnterpriseOperatorViewModel(session)
            {
                Cap = 100,
                WithinCapAmount = 60,
                OverCapAmount = 150
            };

            await operatorVm.InstallCappedRoleCommand.ExecuteAsync(null);
            Assert.True(operatorVm.HasCappedRole, operatorVm.ErrorMessage ?? operatorVm.StatusMessage);

            var balanceBefore = await session.Web3!.Eth.GetBalance.SendRequestAsync(session.PayableTargetAddress!);

            await operatorVm.PayWithinCapCommand.ExecuteAsync(null);
            Assert.Null(operatorVm.ErrorMessage);
            Assert.NotNull(operatorVm.LastReceipt);
            Assert.True(operatorVm.LastReceipt!.UserOpSuccess, operatorVm.LastReceipt.FailureDiagnostic);
            Assert.Equal(balanceBefore.Value + 60, operatorVm.TargetBalance);

            await operatorVm.TryOverCapCommand.ExecuteAsync(null);
            Assert.Null(operatorVm.ErrorMessage);
            Assert.True(operatorVm.OverCapRejected == true, operatorVm.StatusMessage);
        }
    }
}
