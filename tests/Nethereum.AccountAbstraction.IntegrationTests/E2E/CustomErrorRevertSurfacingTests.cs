using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.InProcess;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.TestCustomErrorTarget;
using Nethereum.AccountAbstraction.IntegrationTests.TestCustomErrorTarget.ContractDefinition;
using Nethereum.Contracts;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "AAContractHandler")]
    [Trait("ERC", "4337")]
    public class CustomErrorRevertSurfacingTests
    {
        private readonly DevChainBundlerFixture _fixture;

        public CustomErrorRevertSurfacingTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<(TestCustomErrorTargetService target, string accountAddress)> CreateAccountAndTargetAsync(ulong salt)
        {
            var (accountAddress, accountKey) = await _fixture.CreateFundedAccountAsync(salt);

            var target = await TestCustomErrorTargetService.DeployContractAndGetServiceAsync(
                _fixture.Web3, new TestCustomErrorTargetDeployment());

            target.ChangeContractHandlerToAA(
                accountAddress,
                accountKey,
                new BundlerServiceAdapter(_fixture.BundlerService, DevChainBundlerFixture.CHAIN_ID),
                _fixture.EntryPointService.ContractAddress);

            return (target, accountAddress);
        }

        [Fact]
        public async Task Given_a_target_call_reverts_with_a_custom_error_When_estimating_Then_AAContractHandler_throws_SmartContractCustomErrorRevertException_with_the_typed_fields()
        {
            var (target, accountAddress) = await CreateAccountAndTargetAsync(4301);
            var slotId = new BigInteger(777);

            var ex = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => target.BookSlotRequestAndWaitForReceiptAsync(new BookSlotFunction { SlotId = slotId }));

            Assert.True(ex.ExceptionEncodedData.IsExceptionEncodedDataForError<SlotTakenError>());
            var decoded = ex.ExceptionEncodedData.DecodeExceptionEncodedData<SlotTakenError>();
            Assert.NotNull(decoded);
            Assert.Equal(slotId, decoded.SlotId);
            Assert.Equal(accountAddress.ToLowerInvariant(), decoded.Caller.ToLowerInvariant());
        }

        [Fact]
        public async Task Given_a_target_call_reverts_with_a_standard_reason_When_estimating_Then_the_pre_existing_InvalidOperationException_is_unchanged()
        {
            var (target, _) = await CreateAccountAndTargetAsync(4302);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(
                () => target.FailWithReasonRequestAndWaitForReceiptAsync());
            Assert.Contains("standard fail", ex.Message);
        }
    }
}
