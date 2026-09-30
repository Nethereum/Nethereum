using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Nethereum.AccountAbstraction.AppChain.Configuration;
using Nethereum.AccountAbstraction.AppChain.Deployment;
using Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Modernization;
using Nethereum.AccountAbstraction.AppChain.Services;
using Nethereum.AccountAbstraction.Client;
using Nethereum.AccountAbstraction.Configuration;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget;
using Nethereum.AccountAbstraction.Contracts.Rules.PayableTarget.ContractDefinition;
using Nethereum.AccountAbstraction.ERC7579;
using Nethereum.AccountAbstraction.ERC7579.Modules.MultiGuardian;
using Nethereum.AccountAbstraction.ERC7579.Modules.SmartSession;
using Nethereum.Contracts;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.AccountAbstraction.AppChain.IntegrationTests.E2E.Enterprise
{
    [Collection(AppChainModularBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "E2E-AppChainModernization")]
    [Trait("UseCase", "EnterpriseTier2QuorumOwnerSorting")]
    public class EnterpriseQuorumOwnerSortingE2ETests
    {
        private const int Threshold = 2;
        private const int Tier1Cap = 100;
        private const int Tier2Cap = 1000;
        private const int Tier2Value = 600;

        private readonly AppChainModularBundlerFixture _fixture;

        public EnterpriseQuorumOwnerSortingE2ETests(AppChainModularBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private IAAClient BuildClient()
        {
            var services = new ServiceCollection();
            services.AddNethereumAccountAbstraction(o => o
                .UseWeb3(_fixture.OperatorWeb3)
                .UseDeploymentAddresses(_fixture.Deployment.ToAADeploymentAddresses())
                .UseBundler(_fixture.Bundler));

            return services.BuildServiceProvider().GetRequiredService<IAAClient>();
        }

        [Fact]
        public async Task Given_tier2_owner_addresses_supplied_unsorted_When_installed_through_the_service_Then_a_within_cap_quorum_payment_still_executes()
        {
            var d = _fixture.Deployment;
            var client = BuildClient();
            var depositSelector = new DepositFunction().GetCallData();

            var owner = EthECKey.GenerateKey();
            var accountConfig = new AppChainAccountConfig { Owner = owner.GetPublicAddress(), Salt = 141 };
            var accountAddress = await _fixture.AdminService.EnrollAccountAsync(accountConfig);
            Assert.True(await _fixture.AdminService.IsAccountDeployedAsync(accountAddress));
            Assert.True(await _fixture.AdminService.IsActiveAsync(accountAddress));

            await _fixture.Node.SetBalanceAsync(accountAddress, Nethereum.Web3.Web3.Convert.ToWei(10));

            var salt = new byte[32];
            salt[31] = 141;
            var account = await client.CreateAccountAsync(owner, salt);
            Assert.Equal(accountAddress.ToLowerInvariant(), account.Address.ToLowerInvariant());

            var userSection = new EnterpriseAccountOperatorService(_fixture.OperatorWeb3, client, d, account);

            var operatorKey = EthECKey.GenerateKey();

            var owners = new[] { EthECKey.GenerateKey(), EthECKey.GenerateKey(), EthECKey.GenerateKey() };
            var descendingOwners = owners
                .Select(k => k.GetPublicAddress())
                .OrderByDescending(a => a, StringComparer.OrdinalIgnoreCase)
                .ToList();
            Assert.NotEqual(
                MultiGuardianSignatureBlobBuilder.SortAddressesAscending(descendingOwners).ToList(),
                descendingOwners);

            var tier1Salt = new byte[32]; tier1Salt[31] = 142;
            var tier2Salt = new byte[32]; tier2Salt[31] = 143;

            var tier1Spec = new CappedRoleSpec(
                sessionKeyAddress: operatorKey.GetPublicAddress(),
                targetAddress: _fixture.PayableTarget.ContractAddress,
                functionSelector: depositSelector,
                policyAddress: _fixture.ValueCapCombinator.ContractAddress,
                policyRuleId: _fixture.CapRuleId,
                cap: Tier1Cap,
                salt: tier1Salt);

            var tier2Spec = new QuorumRoleSpec(
                validatorAddress: _fixture.OwnableValidator.ContractAddress,
                ownerAddresses: descendingOwners,
                threshold: Threshold,
                targetAddress: _fixture.PayableTarget.ContractAddress,
                functionSelector: depositSelector,
                policyAddress: _fixture.ValueCapCombinator.ContractAddress,
                policyRuleId: _fixture.CapRuleId,
                cap: Tier2Cap,
                salt: tier2Salt);

            var tieredResult = await userSection.InstallTieredRolesAsync(tier1Spec, tier2Spec);
            Assert.True(tieredResult.Receipt.UserOpSuccess, tieredResult.Receipt.FailureDiagnostic);

            var quorumSigningService = new OwnableValidatorSessionSigningService(
                tieredResult.Tier2PermissionId, new[] { owners[0], owners[1] }, Threshold);
            var quorumValidatorModule = new OwnableValidatorSessionValidatorModule(
                d.Modules.SmartSession, tieredResult.Tier2PermissionId, signatureSlotCount: Threshold);
            var quorumAccount = client.GetAccount(accountAddress, quorumSigningService, quorumValidatorModule);
            var quorumTarget = new PayableTargetService(_fixture.OperatorWeb3, _fixture.PayableTarget.ContractAddress);
            quorumTarget.UseAccountAbstraction(quorumAccount, client);

            var balanceBefore = await GetTargetBalanceAsync();
            var receipt = (AATransactionReceipt)await quorumTarget.DepositRequestAndWaitForReceiptAsync(
                new DepositFunction { AmountToSend = Tier2Value });
            Assert.True(receipt.UserOpSuccess,
                "Expected the 2-of-3 quorum payment to execute even though the owners were supplied " +
                $"unsorted - the service must normalize them before install. Diagnostic: {receipt.FailureDiagnostic}");
            var balanceAfter = await GetTargetBalanceAsync();
            Assert.Equal(balanceBefore.Value + Tier2Value, balanceAfter.Value);
        }

        private Task<Nethereum.Hex.HexTypes.HexBigInteger> GetTargetBalanceAsync() =>
            _fixture.OperatorWeb3.Eth.GetBalance.SendRequestAsync(_fixture.PayableTarget.ContractAddress);
    }
}
