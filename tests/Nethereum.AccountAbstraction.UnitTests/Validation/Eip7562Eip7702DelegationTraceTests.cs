using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.EVM;
using Nethereum.EVM.Precompiles;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.RPC.Eth.DTOs;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class Eip7562Eip7702DelegationTraceTests
    {
        private const string EntryPointAddress = "0x433709009B8330FDa32311DF1C2AFA402eD8D009";

        private const string SenderAddress = "0x2222222222222222222222222222222222222222";

        private const string DelegateAddress = "0x3333333333333333333333333333333333333333";
        private static readonly byte[] DelegateRuntime = "60005460005260206000f3".HexToByteArray();

        private readonly InMemoryNodeDataService _nodeDataService = new();
        private readonly ERC7562SimulationService _simulationService;

        public Eip7562Eip7702DelegationTraceTests()
        {
            _simulationService = new ERC7562SimulationService(_nodeDataService, DefaultHardforkConfigs.Osaka);
        }

        [Fact]
        [Trait("Category", "EIP7702-4337")]
        [Trait("Rule", "AA20")]
        public async Task Given_NotYetDelegated7702Sender_WithDelegationInjected_When_Simulated_Then_NoAA20_AndDelegateExecutes()
        {
            await _nodeDataService.SetCodeAsync(DelegateAddress, DelegateRuntime);
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = CreateTestUserOp(SenderAddress);
            var sender = Erc4337Entity.CreateSender(SenderAddress, isStaked: false);

            var auth = new Authorisation { Address = DelegateAddress, ChainId = new HexBigInteger(0) };

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender,
                chainId: 1,
                eip7702Auth: auth);

            Assert.DoesNotContain(result.Violations, v => v.Rule == "AA20");
            Assert.True(result.IsValid,
                "Expected a not-yet-delegated 7702 sender to validate through the trace once the " +
                "delegation designator is injected. Violations: " +
                string.Join("; ", result.Violations.Select(v => $"{v.Rule}: {v.Message} ({v.Entity}@{v.Address})")));
            Assert.True(result.OpcodeExecutions.Count > 0,
                "Expected a non-empty opcode trace — an empty trace means the delegate's code never " +
                "ran at the sender's address (the designator wasn't resolved).");
        }

        [Fact]
        [Trait("Category", "EIP7702-4337")]
        [Trait("Rule", "AA20")]
        public async Task Given_NotYetDelegated7702Sender_WithoutAuth_When_Simulated_Then_AA20()
        {
            await _nodeDataService.SetCodeAsync(DelegateAddress, DelegateRuntime);
            await _nodeDataService.SetBalanceAsync(EntryPointAddress, BigInteger.Parse("1000000000000000000"));

            var userOp = CreateTestUserOp(SenderAddress);
            var sender = Erc4337Entity.CreateSender(SenderAddress, isStaked: false);

            var result = await _simulationService.ValidateUserOperationAsync(
                userOp,
                EntryPointAddress,
                sender,
                chainId: 1);

            Assert.False(result.IsValid);
            Assert.Contains(result.Violations, v => v.Rule == "AA20");
        }

        private static PackedUserOperationDTO CreateTestUserOp(string senderAddress)
        {
            return new PackedUserOperationDTO
            {
                Sender = senderAddress,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 50000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };
        }
    }
}
