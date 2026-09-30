using System.Threading.Tasks;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.EVM.UnitTests.Gas
{
    public class Eip7702CodeResolutionFailureStateGasResetTests
    {
        private const string Target = "0x2222222222222222222222222222222222222222";
        private const string Delegate = "0x3333333333333333333333333333333333333333";

        private static byte[] DelegationCode(string delegate_)
        {
            var code = new byte[23];
            code[0] = 0xef; code[1] = 0x01; code[2] = 0x00;
            delegate_.HexToByteArray().CopyTo(code, 3);
            return code;
        }

        [Fact]
        public async Task Given_CodeResolutionFails_When_PriorStateGasWasCharged_Then_StateGasIsFullyReset()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());

            var prepPhaseSnapshotId = stateService.TakeSnapshot();

            const string authority = "0x4444444444444444444444444444444444444444";
            var authorityAccount = stateService.CreateOrGetAccountExecutionState(authority);
            authorityAccount.Nonce = 1;
            authorityAccount.Code = DelegationCode(Delegate);

            var transactionSnapshotId = stateService.TakeSnapshot();

            var ctx = new TransactionExecutionContext
            {
                To = Target,
                IsContractCreation = false,
                Code = DelegationCode(Delegate),
                ExecutionState = stateService,
                TransactionSnapshotId = transactionSnapshotId,
                PrepPhaseSnapshotId = prepPhaseSnapshotId,
                StateGasReservoir = 10_000,
                StateGas = new StateGasAccount
                {
                    ReservoirRemaining = 7_000,
                    FromReservoir = 3_000,
                    SpilledIntoExecution = 250
                },
                PreDispatchExecutionGasCharged = 400,
                ExecutionGasGrant = 450
            };

            await Eip7702AuthorizationApplication.ResolveCodeIfDelegatedAsync(ctx, chargeDelegationAccess: true);

            Assert.True(ctx.CodeResolutionFailed);

            Assert.Equal(ctx.StateGasReservoir, ctx.StateGas.ReservoirRemaining);
            Assert.Equal(0, ctx.StateGas.FromReservoir);
            Assert.Equal(0, ctx.StateGas.SpilledIntoExecution);
            Assert.Equal(0, ctx.PreDispatchExecutionGasCharged);
            Assert.Equal(0, ctx.StateGasUsed);

            var authorityCodeAfter = await stateService.GetCodeAsync(authority);
            Assert.False(Eip7702DelegationUtils.IsDelegatedCode(authorityCodeAfter));
            var authorityNonceAfter = await stateService.GetNonceAsync(authority);
            Assert.Equal(Nethereum.Util.EvmUInt256.Zero, authorityNonceAfter);
        }

        [Fact]
        public async Task Given_CodeResolutionSucceeds_Then_StateGasIsUntouched()
        {
            var stateService = new ExecutionStateService(new MockNodeDataService());
            var prepPhaseSnapshotId = stateService.TakeSnapshot();
            var transactionSnapshotId = stateService.TakeSnapshot();

            var ctx = new TransactionExecutionContext
            {
                To = Target,
                IsContractCreation = false,
                Code = DelegationCode(Delegate),
                ExecutionState = stateService,
                TransactionSnapshotId = transactionSnapshotId,
                PrepPhaseSnapshotId = prepPhaseSnapshotId,
                StateGasReservoir = 10_000,
                StateGas = new StateGasAccount
                {
                    ReservoirRemaining = 7_000,
                    FromReservoir = 3_000,
                    SpilledIntoExecution = 250
                },
                PreDispatchExecutionGasCharged = 400,
                ExecutionGasGrant = 1_000_000
            };

            await Eip7702AuthorizationApplication.ResolveCodeIfDelegatedAsync(ctx, chargeDelegationAccess: true);

            Assert.False(ctx.CodeResolutionFailed);
            Assert.Equal(3_000, ctx.StateGas.FromReservoir);
            Assert.Equal(250, ctx.StateGas.SpilledIntoExecution);
            Assert.Equal(3_400, ctx.PreDispatchExecutionGasCharged);
        }
    }
}
