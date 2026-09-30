using System;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    public class ERC7562ValidationRulesTests
    {
        private readonly ERC7562RuleEnforcer _enforcer = new ERC7562RuleEnforcer();

        #region Opcode Restrictions [OP-011] - Always Forbidden

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-011")]
        [InlineData(Instruction.ORIGIN)]
        [InlineData(Instruction.GASPRICE)]
        [InlineData(Instruction.BLOCKHASH)]
        [InlineData(Instruction.COINBASE)]
        [InlineData(Instruction.TIMESTAMP)]
        [InlineData(Instruction.NUMBER)]
        [InlineData(Instruction.DIFFICULTY)]
        [InlineData(Instruction.GASLIMIT)]
        [InlineData(Instruction.BASEFEE)]
        [InlineData(Instruction.SELFDESTRUCT)]
        public void Given_ForbiddenOpcode_When_Validated_Then_ReturnsOP011Violation(Instruction opcode)
        {
            var context = CreateValidationContext();

            var violation = _enforcer.ValidateOpcode(opcode, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-011", violation.Rule);
        }

        #endregion

        #region GAS Opcode [OP-012]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-012")]
        public void Given_GASOpcode_When_NotFollowedByCall_Then_ReturnsOP012Violation()
        {
            var context = CreateValidationContext();

            var violation = _enforcer.ValidateOpcode(Instruction.GAS, Instruction.ADD, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-012", violation.Rule);
            Assert.Contains("uses banned opcode: gas", violation.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-012")]
        [InlineData(Instruction.CALL)]
        [InlineData(Instruction.STATICCALL)]
        [InlineData(Instruction.DELEGATECALL)]
        [InlineData(Instruction.CALLCODE)]
        public void Given_GASOpcode_When_FollowedByCall_Then_NoViolation(Instruction nextOpcode)
        {
            var context = CreateValidationContext();

            var violation = _enforcer.ValidateOpcode(Instruction.GAS, nextOpcode, context);

            Assert.Null(violation);
        }

        #endregion

        #region Unassigned Opcodes [OP-013]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-013")]
        public void Given_ValidOpcode_When_Checked_Then_IsRecognized()
        {
            Assert.True(ForbiddenOpcodes.IsValidOpcode(Instruction.ADD));
            Assert.True(ForbiddenOpcodes.IsValidOpcode(Instruction.SSTORE));
            Assert.True(ForbiddenOpcodes.IsValidOpcode(Instruction.CALL));
        }

        #endregion

        #region CREATE/CREATE2 Restrictions [OP-031, OP-032]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-031")]
        public void Given_CREATE2_When_UsedByUnstakedFactoryMoreThanOnce_Then_ReturnsOP031Violation()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Factory;
            context.IsDeploymentPhase = true;
            context.Create2Count = 1;

            var violation = _enforcer.ValidateOpcode(Instruction.CREATE2, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-031", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-031")]
        public void Given_CREATE2_When_UsedByStakedEntity_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Factory = new Erc4337Entity { Address = "0xFactory", IsStaked = true };
            context.CurrentEntity = EntityType.Factory;
            context.CurrentAddress = "0xFactory";
            context.Create2Count = 5;

            var violation = _enforcer.ValidateOpcode(Instruction.CREATE2, null, context);

            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-032")]
        public void Given_CREATE_When_UsedBySenderWithFactory_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Factory = new Erc4337Entity { Address = "0xFactory", IsStaked = false };
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = false };
            context.CurrentEntity = EntityType.Sender;
            context.CurrentAddress = "0xSender";

            var violation = _enforcer.ValidateOpcode(Instruction.CREATE, null, context);

            Assert.Null(violation);
        }

        #endregion

        #region Code Access [OP-041, OP-042]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-041")]
        public void Given_ExtCodeAccess_When_AddressHasNoCode_Then_ReturnsOP041Violation()
        {
            var context = CreateValidationContext();
            context.IsDeploymentPhase = false;

            var violation = _enforcer.ValidateCodeAccess("0xNoCode", hasCode: false, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-041", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-042")]
        public void Given_ExtCodeAccess_When_SenderDuringDeployment_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender" };
            context.IsDeploymentPhase = true;

            var violation = _enforcer.ValidateCodeAccess("0xSender", hasCode: false, context);

            Assert.Null(violation);
        }

        #endregion

        #region EntryPoint Access [OP-051 through OP-055]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-052")]
        public void Given_DepositToCall_When_FromSender_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;

            var depositToData = "b760faf9".HexToByteArray();

            var violation = _enforcer.ValidateCall("0xSender", context.EntryPointAddress, 0, depositToData, context);

            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-052")]
        public void Given_DepositToCall_When_FromPaymaster_Then_ReturnsOP052Violation()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Paymaster;

            var depositToData = "b760faf9".HexToByteArray();

            var violation = _enforcer.ValidateCall("0xPaymaster", context.EntryPointAddress, 0, depositToData, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-052", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-054")]
        public void Given_IncrementNonceCall_When_FromSender_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;

            var incrementNonceData = "0bd28e3b".HexToByteArray();

            var violation = _enforcer.ValidateCall("0xSender", context.EntryPointAddress, 0, incrementNonceData, context);

            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-055")]
        public void Given_UnauthorizedEntryPointCall_Then_ReturnsOP055Violation()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;

            var unknownSelector = "12345678".HexToByteArray();

            var violation = _enforcer.ValidateCall("0xSender", context.EntryPointAddress, 0, unknownSelector, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-055", violation.Rule);
        }

        #endregion

        #region CALL Restrictions [OP-061]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-061")]
        public void Given_CallWithValue_When_NotToEntryPoint_Then_ReturnsOP061Violation()
        {
            var context = CreateValidationContext();
            BigInteger value = 1000000;

            var violation = _enforcer.ValidateCall("0xFrom", "0xOtherAddress", value, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-061", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-061")]
        public void Given_CallWithValue_When_ToEntryPoint_Then_NoViolation()
        {
            var context = CreateValidationContext();
            BigInteger value = 1000000;

            var violation = _enforcer.ValidateCall("0xFrom", context.EntryPointAddress, value, null, context);

            Assert.Null(violation);
        }

        #endregion

        #region Precompile Access [OP-062]

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        [InlineData(0x01)]
        [InlineData(0x02)]
        [InlineData(0x03)]
        [InlineData(0x04)]
        [InlineData(0x05)]
        [InlineData(0x06)]
        [InlineData(0x07)]
        [InlineData(0x08)]
        [InlineData(0x09)]
        [InlineData(0x0a)]
        [InlineData(0x0b)]
        [InlineData(0x0c)]
        [InlineData(0x0d)]
        [InlineData(0x0e)]
        [InlineData(0x0f)]
        [InlineData(0x10)]
        [InlineData(0x11)]
        public void Given_AllowedPrecompile_When_Called_Then_NoViolation(int precompileAddress)
        {
            var context = CreateValidationContext();

            var violation = _enforcer.ValidatePrecompileCall(precompileAddress, context);

            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_RIP7212Precompile_When_AllowedInConfig_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.AllowRip7212Precompile = true;

            var violation = _enforcer.ValidatePrecompileCall(0x100, context);

            Assert.Null(violation);
        }

        /// <summary>
        /// The twin for the upper bound. ERC-7562 [OP-062] names <i>"The core precompiles
        /// <c>0x1</c>-<c>0x11</c>"</i>, so 0x12 is outside the list the ERC accepts and an
        /// allow-list widened past the ERC would take it.
        /// </summary>
        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_APrecompileJustPastTheErcList_When_Called_Then_ReturnsOP062Violation()
        {
            var context = CreateValidationContext();

            Assert.NotNull(_enforcer.ValidatePrecompileCall(0x12, context));
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_UnknownPrecompile_When_Called_Then_ReturnsOP062Violation()
        {
            var context = CreateValidationContext();

            var violation = _enforcer.ValidatePrecompileCall(0xFF, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-062", violation.Rule);
        }

        #endregion

        #region Balance Access [OP-080]

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-080")]
        [InlineData(Instruction.BALANCE)]
        [InlineData(Instruction.SELFBALANCE)]
        public void Given_BalanceOpcode_When_EntityNotStaked_Then_ReturnsOP080Violation(Instruction opcode)
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = false };
            context.CurrentEntity = EntityType.Sender;

            var violation = _enforcer.ValidateOpcode(opcode, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-080", violation.Rule);
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-080")]
        [InlineData(Instruction.BALANCE)]
        [InlineData(Instruction.SELFBALANCE)]
        public void Given_BalanceOpcode_When_EntityIsStaked_Then_NoViolation(Instruction opcode)
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = true };
            context.CurrentEntity = EntityType.Sender;

            var violation = _enforcer.ValidateOpcode(opcode, null, context);

            Assert.Null(violation);
        }

        #endregion

        #region Storage Rules [STO-010]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-010")]
        public void Given_SenderAccessingOwnStorage_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender" };
            context.CurrentEntity = EntityType.Sender;

            var violation = _enforcer.ValidateStorageAccess("0xSender", 0, isWrite: true, isTransient: false, context);

            Assert.Null(violation);
        }

        #endregion

        #region Associated Storage [STO-021, STO-022]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-021")]
        public void Given_AssociatedStorageAccess_When_FactoryIsStaked_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Factory = new Erc4337Entity { Address = "0xFactory", IsStaked = true };
            context.Sender = new Erc4337Entity { Address = "0xSender" };
            context.CurrentEntity = EntityType.Factory;
            context.IsDeploymentPhase = true;
            context.TrackAssociatedSlot("0xOther", 123);

            var violation = _enforcer.ValidateStorageAccess("0xOther", 123, isWrite: false, isTransient: false, context);

            Assert.Null(violation);
        }

        #endregion

        #region Staked Entity Privileges [STO-031, STO-032, STO-033]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-033")]
        public void Given_StakedEntity_When_ReadingNonEntityStorage_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = true };
            context.CurrentEntity = EntityType.Sender;

            var violation = _enforcer.ValidateStorageAccess("0xRandomContract", 42, isWrite: false, isTransient: false, context);

            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-031")]
        public void Given_StakedEntity_When_WritingAssociatedStorage_Then_NoViolation()
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = true };
            context.CurrentEntity = EntityType.Sender;
            context.TrackAssociatedSlot("0xOther", 99);

            var violation = _enforcer.ValidateStorageAccess("0xOther", 99, isWrite: true, isTransient: false, context);

            Assert.Null(violation);
        }

        #endregion

        #region Associated Storage Calculation

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "AssociatedStorage")]
        public void Given_MappingSlot_When_ContainsSenderAddress_Then_IsAssociated()
        {
            var calculator = new AssociatedStorageCalculator();
            var senderAddress = "0x1234567890123456789012345678901234567890";

            calculator.RegisterSenderSlot(senderAddress, 0);

            var associatedSlots = calculator.GetAssociatedSlots();
            Assert.NotEmpty(associatedSlots);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "AssociatedStorage")]
        public void Given_KeccakPreimage_When_ContainsAddress_Then_TracksAssociation()
        {
            var calculator = new AssociatedStorageCalculator();
            var senderAddress = "0x1234567890123456789012345678901234567890";

            var preimage = new byte[64];
            var addressBytes = senderAddress.HexToByteArray();
            Array.Copy(addressBytes, 0, preimage, 12, 20);

            var resultHash = BigInteger.Parse("12345678901234567890");
            calculator.TrackKeccakFromHash(preimage, resultHash);

            Assert.True(calculator.IsAssociatedSlot("0xContract", resultHash, senderAddress));
        }

        #endregion

        #region Violation Message Wording (bundler-spec-tests parity)


        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-011")]
        [InlineData(EntityType.Sender, "account")]
        [InlineData(EntityType.Paymaster, "paymaster")]
        [InlineData(EntityType.Factory, "factory")]
        public void Given_OP011BannedOpcode_When_Validated_Then_MessageMatchesSpecWording(EntityType entity, string expectedEntityName)
        {
            var context = CreateValidationContext();
            context.CurrentEntity = entity;

            var violation = _enforcer.ValidateOpcode(Instruction.ORIGIN, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-011", violation!.Rule);
            Assert.Contains($"{expectedEntityName} uses banned opcode: ORIGIN", violation.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-080")]
        public void Given_OP080UnstakedBalanceOpcode_When_Validated_Then_MessageMatchesSpecWording()
        {
            var context = CreateValidationContext();
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = false };
            context.CurrentEntity = EntityType.Sender;

            var violation = _enforcer.ValidateOpcode(Instruction.BALANCE, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-080", violation!.Rule);
            Assert.Contains("account uses banned opcode: balance", violation.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-032")]
        public void Given_OP032UnauthorizedCreate_When_Validated_Then_MessageMatchesSpecWording()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;
            context.Sender = new Erc4337Entity { Address = "0xSender", IsStaked = false };

            var violation = _enforcer.ValidateOpcode(Instruction.CREATE, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-032", violation!.Rule);
            Assert.Contains("account uses banned opcode: create", violation.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-031")]
        public void Given_OP031Create2ReusedByFactory_When_Validated_Then_MessageMatchesSpecWording()
        {
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Factory;
            context.IsDeploymentPhase = true;
            context.Create2Count = 1;

            var violation = _enforcer.ValidateOpcode(Instruction.CREATE2, null, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-031", violation!.Rule);
            Assert.Contains("factory uses banned opcode: create2", violation.Message, StringComparison.OrdinalIgnoreCase);
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-012")]
        [InlineData(EntityType.Sender, "account")]
        [InlineData(EntityType.Paymaster, "paymaster")]
        public void Given_LoneGASOpcode_When_Validated_Then_MessageMatchesSpecWording(EntityType entity, string expectedEntityName)
        {
            var context = CreateValidationContext();
            context.CurrentEntity = entity;

            var violation = _enforcer.ValidateOpcode(Instruction.GAS, Instruction.ADD, context);

            Assert.NotNull(violation);
            Assert.Equal("OP-012", violation!.Rule);
            Assert.Contains($"{expectedEntityName} uses banned opcode: GAS", violation.Message, StringComparison.OrdinalIgnoreCase);
        }

        #endregion

        #region Helper Methods

        private static ERC7562ValidationContext CreateValidationContext()
        {
            return new ERC7562ValidationContext
            {
                EntryPointAddress = "0x5ff137d4b0fdcd49dca30c7cf57e578a026d2789",
                Sender = new Erc4337Entity { Address = "0xSender", IsStaked = false },
                CurrentEntity = EntityType.Sender
            };
        }

        #endregion
    }

    public class ForbiddenOpcodesTests
    {
        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-011")]
        [InlineData(Instruction.ORIGIN)]
        [InlineData(Instruction.GASPRICE)]
        [InlineData(Instruction.COINBASE)]
        [InlineData(Instruction.TIMESTAMP)]
        [InlineData(Instruction.NUMBER)]
        [InlineData(Instruction.GASLIMIT)]
        [InlineData(Instruction.BASEFEE)]
        [InlineData(Instruction.SELFDESTRUCT)]
        public void Given_AlwaysForbiddenOpcode_Then_IsAlwaysForbiddenReturnsTrue(Instruction opcode)
        {
            Assert.True(ForbiddenOpcodes.IsAlwaysForbidden(opcode));
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [InlineData(Instruction.ADD)]
        [InlineData(Instruction.MUL)]
        [InlineData(Instruction.SLOAD)]
        [InlineData(Instruction.SSTORE)]
        [InlineData(Instruction.CALL)]
        [InlineData(Instruction.RETURN)]
        public void Given_AllowedOpcode_Then_IsAlwaysForbiddenReturnsFalse(Instruction opcode)
        {
            Assert.False(ForbiddenOpcodes.IsAlwaysForbidden(opcode));
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-080")]
        [InlineData(Instruction.BALANCE)]
        [InlineData(Instruction.SELFBALANCE)]
        public void Given_StakingRequiredOpcode_Then_RequiresStakingReturnsTrue(Instruction opcode)
        {
            Assert.True(ForbiddenOpcodes.RequiresStaking(opcode));
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [InlineData(Instruction.CALL)]
        [InlineData(Instruction.STATICCALL)]
        [InlineData(Instruction.DELEGATECALL)]
        [InlineData(Instruction.CALLCODE)]
        public void Given_CallOpcode_Then_IsCallOpcodeReturnsTrue(Instruction opcode)
        {
            Assert.True(ForbiddenOpcodes.IsCallOpcode(opcode));
        }

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        [InlineData(0x01, true)]
        [InlineData(0x02, true)]
        [InlineData(0x09, true)]
        [InlineData(0x0a, true)]
        [InlineData(0xFF, false)]
        [InlineData(0x20, false)]
        public void Given_PrecompileAddress_Then_IsAllowedPrecompileReturnsCorrectly(int address, bool expected)
        {
            Assert.Equal(expected, ForbiddenOpcodes.IsAllowedPrecompile(address, includeRip7212: false));
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_RIP7212Precompile_When_Allowed_Then_ReturnsTrue()
        {
            Assert.True(ForbiddenOpcodes.IsAllowedPrecompile(0x100, includeRip7212: true));
            Assert.False(ForbiddenOpcodes.IsAllowedPrecompile(0x100, includeRip7212: false));
        }
    }

    internal static class TestExtensions
    {
        public static byte[] HexToByteArray(this string hex)
        {
            hex = hex.Replace("0x", "").Replace("0X", "");
            if (hex.Length % 2 != 0)
                hex = "0" + hex;

            var bytes = new byte[hex.Length / 2];
            for (int i = 0; i < bytes.Length; i++)
            {
                bytes[i] = Convert.ToByte(hex.Substring(i * 2, 2), 16);
            }
            return bytes;
        }
    }
}
