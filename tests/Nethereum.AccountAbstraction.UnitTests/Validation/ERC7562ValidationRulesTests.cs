using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler.Validation.ERC7562;
using Nethereum.EVM;
using Xunit;

namespace Nethereum.AccountAbstraction.UnitTests.Validation
{
    /// <summary>
    /// ERC-7562: Account Abstraction Validation Scope Rules Tests
    ///
    /// These tests validate the Nethereum implementation against the official ERC-7562 specification.
    /// Each test is tagged with the specific rule it validates.
    ///
    /// Spec: https://eips.ethereum.org/EIPS/eip-7562
    /// </summary>
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
        [InlineData(Instruction.DIFFICULTY)] // PREVRANDAO
        [InlineData(Instruction.GASLIMIT)]
        [InlineData(Instruction.BASEFEE)]
        [InlineData(Instruction.SELFDESTRUCT)]
        public void Given_ForbiddenOpcode_When_Validated_Then_ReturnsOP011Violation(Instruction opcode)
        {
            // GIVEN: A forbidden opcode from ERC-7562 OP-011
            var context = CreateValidationContext();

            // WHEN: The opcode is validated
            var violation = _enforcer.ValidateOpcode(opcode, null, context);

            // THEN: Returns OP-011 violation
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
            // SPEC: GAS opcode permitted only when immediately followed by *CALL
            var context = CreateValidationContext();

            // WHEN: GAS is followed by ADD (not a CALL)
            var violation = _enforcer.ValidateOpcode(Instruction.GAS, Instruction.ADD, context);

            // THEN: Returns OP-012 violation
            Assert.NotNull(violation);
            Assert.Equal("OP-012", violation.Rule);
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
            // SPEC: GAS is allowed when immediately followed by CALL variants
            var context = CreateValidationContext();

            // WHEN: GAS is followed by a CALL opcode
            var violation = _enforcer.ValidateOpcode(Instruction.GAS, nextOpcode, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Unassigned Opcodes [OP-013]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-013")]
        public void Given_ValidOpcode_When_Checked_Then_IsRecognized()
        {
            // SPEC: Unassigned opcodes are always forbidden
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
            // SPEC: CREATE2 allowed exactly once in deployment for sender by unstaked factory
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Factory;
            context.IsDeploymentPhase = true;
            context.Create2Count = 1; // Already used once

            // WHEN: CREATE2 is used again
            var violation = _enforcer.ValidateOpcode(Instruction.CREATE2, null, context);

            // THEN: Returns OP-031 violation
            Assert.NotNull(violation);
            Assert.Equal("OP-031", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-031")]
        public void Given_CREATE2_When_UsedByStakedEntity_Then_NoViolation()
        {
            // SPEC: Staked entities can use CREATE2 freely
            var context = CreateValidationContext();
            context.Factory = new EntityInfo { Address = "0xFactory", IsStaked = true };
            context.CurrentEntity = EntityType.Factory;
            context.Create2Count = 5; // Used multiple times

            // WHEN: CREATE2 is used by staked factory
            var violation = _enforcer.ValidateOpcode(Instruction.CREATE2, null, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-032")]
        public void Given_CREATE_When_UsedBySenderWithFactory_Then_NoViolation()
        {
            // SPEC: CREATE permitted for sender with factory
            var context = CreateValidationContext();
            context.Factory = new EntityInfo { Address = "0xFactory", IsStaked = false };
            context.Sender = new EntityInfo { Address = "0xSender", IsStaked = false };
            context.CurrentEntity = EntityType.Sender;

            // WHEN: CREATE is used by sender
            var violation = _enforcer.ValidateOpcode(Instruction.CREATE, null, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Code Access [OP-041, OP-042]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-041")]
        public void Given_ExtCodeAccess_When_AddressHasNoCode_Then_ReturnsOP041Violation()
        {
            // SPEC: EXTCODE* to address without code is forbidden
            var context = CreateValidationContext();
            context.IsDeploymentPhase = false;

            // WHEN: Accessing code of address without deployed code
            var violation = _enforcer.ValidateCodeAccess("0xNoCode", hasCode: false, context);

            // THEN: Returns OP-041 violation
            Assert.NotNull(violation);
            Assert.Equal("OP-041", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-042")]
        public void Given_ExtCodeAccess_When_SenderDuringDeployment_Then_NoViolation()
        {
            // SPEC: Exception for sender address during factory deployment
            var context = CreateValidationContext();
            context.Sender = new EntityInfo { Address = "0xSender" };
            context.IsDeploymentPhase = true;

            // WHEN: Accessing sender code during deployment (before it exists)
            var violation = _enforcer.ValidateCodeAccess("0xSender", hasCode: false, context);

            // THEN: No violation (exception applies)
            Assert.Null(violation);
        }

        #endregion

        #region EntryPoint Access [OP-051 through OP-055]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-052")]
        public void Given_DepositToCall_When_FromSender_Then_NoViolation()
        {
            // SPEC: depositTo(address) allowed from sender or factory
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;

            var depositToData = "b760faf9".HexToByteArray(); // depositTo selector

            // WHEN: Sender calls depositTo
            var violation = _enforcer.ValidateCall("0xSender", context.EntryPointAddress, 0, depositToData, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-052")]
        public void Given_DepositToCall_When_FromPaymaster_Then_ReturnsOP052Violation()
        {
            // SPEC: depositTo only allowed from sender or factory
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Paymaster;

            var depositToData = "b760faf9".HexToByteArray();

            // WHEN: Paymaster calls depositTo
            var violation = _enforcer.ValidateCall("0xPaymaster", context.EntryPointAddress, 0, depositToData, context);

            // THEN: Returns OP-052 violation
            Assert.NotNull(violation);
            Assert.Equal("OP-052", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-054")]
        public void Given_IncrementNonceCall_When_FromSender_Then_NoViolation()
        {
            // SPEC: incrementNonce(uint192) allowed from sender only
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;

            var incrementNonceData = "0bd28e3b".HexToByteArray();

            // WHEN: Sender calls incrementNonce
            var violation = _enforcer.ValidateCall("0xSender", context.EntryPointAddress, 0, incrementNonceData, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-055")]
        public void Given_UnauthorizedEntryPointCall_Then_ReturnsOP055Violation()
        {
            // SPEC: Any other access to EntryPoint is forbidden
            var context = CreateValidationContext();
            context.CurrentEntity = EntityType.Sender;

            var unknownSelector = "12345678".HexToByteArray();

            // WHEN: Calling unknown EntryPoint method
            var violation = _enforcer.ValidateCall("0xSender", context.EntryPointAddress, 0, unknownSelector, context);

            // THEN: Returns OP-055 violation
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
            // SPEC: CALL with value forbidden except to EntryPoint
            var context = CreateValidationContext();
            BigInteger value = 1000000;

            // WHEN: Call with value to non-EntryPoint address
            var violation = _enforcer.ValidateCall("0xFrom", "0xOtherAddress", value, null, context);

            // THEN: Returns OP-061 violation
            Assert.NotNull(violation);
            Assert.Equal("OP-061", violation.Rule);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-061")]
        public void Given_CallWithValue_When_ToEntryPoint_Then_NoViolation()
        {
            // SPEC: CALL with value allowed to EntryPoint
            var context = CreateValidationContext();
            BigInteger value = 1000000;

            // WHEN: Call with value to EntryPoint
            var violation = _enforcer.ValidateCall("0xFrom", context.EntryPointAddress, value, null, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Precompile Access [OP-062]

        [Theory]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        [InlineData(0x01)] // ecrecover
        [InlineData(0x02)] // sha256
        [InlineData(0x03)] // ripemd160
        [InlineData(0x04)] // identity
        [InlineData(0x05)] // modexp
        [InlineData(0x06)] // ecadd
        [InlineData(0x07)] // ecmul
        [InlineData(0x08)] // ecpairing
        [InlineData(0x09)] // blake2f
        [InlineData(0x0a)] // kzg point evaluation
        public void Given_AllowedPrecompile_When_Called_Then_NoViolation(int precompileAddress)
        {
            // SPEC: Only core precompiles (0x1-0x0a) are allowed
            var context = CreateValidationContext();

            // WHEN: Calling allowed precompile
            var violation = _enforcer.ValidatePrecompileCall(precompileAddress, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_RIP7212Precompile_When_AllowedInConfig_Then_NoViolation()
        {
            // SPEC: RIP-7212 secp256r1 precompile (0x100) allowed if configured
            var context = CreateValidationContext();
            context.AllowRip7212Precompile = true;

            // WHEN: Calling RIP-7212 precompile
            var violation = _enforcer.ValidatePrecompileCall(0x100, context);

            // THEN: No violation when allowed
            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_UnknownPrecompile_When_Called_Then_ReturnsOP062Violation()
        {
            // SPEC: Unknown precompiles are forbidden
            var context = CreateValidationContext();

            // WHEN: Calling unknown precompile
            var violation = _enforcer.ValidatePrecompileCall(0xFF, context);

            // THEN: Returns OP-062 violation
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
            // SPEC: BALANCE and SELFBALANCE restricted to staked entities
            var context = CreateValidationContext();
            context.Sender = new EntityInfo { Address = "0xSender", IsStaked = false };
            context.CurrentEntity = EntityType.Sender;

            // WHEN: Unstaked entity uses balance opcode
            var violation = _enforcer.ValidateOpcode(opcode, null, context);

            // THEN: Returns OP-080 violation
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
            // SPEC: Staked entities can use balance opcodes
            var context = CreateValidationContext();
            context.Sender = new EntityInfo { Address = "0xSender", IsStaked = true };
            context.CurrentEntity = EntityType.Sender;

            // WHEN: Staked entity uses balance opcode
            var violation = _enforcer.ValidateOpcode(opcode, null, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Storage Rules [STO-010]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-010")]
        public void Given_SenderAccessingOwnStorage_Then_NoViolation()
        {
            // SPEC: Sender can always access own storage
            var context = CreateValidationContext();
            context.Sender = new EntityInfo { Address = "0xSender" };
            context.CurrentEntity = EntityType.Sender;

            // WHEN: Sender accesses own storage
            var violation = _enforcer.ValidateStorageAccess("0xSender", 0, isWrite: true, isTransient: false, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Associated Storage [STO-021, STO-022]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-021")]
        public void Given_AssociatedStorageAccess_When_FactoryIsStaked_Then_NoViolation()
        {
            // SPEC: Associated storage access permitted when factory is staked
            var context = CreateValidationContext();
            context.Factory = new EntityInfo { Address = "0xFactory", IsStaked = true };
            context.Sender = new EntityInfo { Address = "0xSender" };
            context.CurrentEntity = EntityType.Factory;
            context.IsDeploymentPhase = true;
            context.TrackAssociatedSlot("0xOther", 123);

            // WHEN: Accessing associated storage during deployment
            var violation = _enforcer.ValidateStorageAccess("0xOther", 123, isWrite: false, isTransient: false, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Staked Entity Privileges [STO-031, STO-032, STO-033]

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-033")]
        public void Given_StakedEntity_When_ReadingNonEntityStorage_Then_NoViolation()
        {
            // SPEC: Staked entities can read any non-entity storage
            var context = CreateValidationContext();
            context.Sender = new EntityInfo { Address = "0xSender", IsStaked = true };
            context.CurrentEntity = EntityType.Sender;

            // WHEN: Staked sender reads external storage
            var violation = _enforcer.ValidateStorageAccess("0xRandomContract", 42, isWrite: false, isTransient: false, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "STO-031")]
        public void Given_StakedEntity_When_WritingAssociatedStorage_Then_NoViolation()
        {
            // SPEC: Staked entities can read/write associated storage
            var context = CreateValidationContext();
            context.Sender = new EntityInfo { Address = "0xSender", IsStaked = true };
            context.CurrentEntity = EntityType.Sender;
            context.TrackAssociatedSlot("0xOther", 99);

            // WHEN: Staked sender writes to associated storage
            var violation = _enforcer.ValidateStorageAccess("0xOther", 99, isWrite: true, isTransient: false, context);

            // THEN: No violation
            Assert.Null(violation);
        }

        #endregion

        #region Bundle Storage Conflict Detection

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "BundleConflict")]
        public void Given_TwoUserOps_When_SameSender_Then_DetectsConflict()
        {
            // SPEC: Same sender cannot have multiple operations in bundle
            var detector = new BundleStorageConflictDetector();
            var profiles = new List<UserOpStorageProfile>
            {
                new UserOpStorageProfile { UserOpHash = "0xA", SenderAddress = "0xSender" },
                new UserOpStorageProfile { UserOpHash = "0xB", SenderAddress = "0xSender" }
            };

            // WHEN: Detecting conflicts
            var conflicts = detector.DetectConflicts(profiles);

            // THEN: Sender conflict detected
            Assert.Contains(conflicts, c => c.Type == StorageConflictType.SenderConflict);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "BundleConflict")]
        public void Given_TwoUserOps_When_WriteWriteConflict_Then_DetectsConflict()
        {
            // SPEC: Write-write conflicts on same slot are forbidden
            var detector = new BundleStorageConflictDetector();

            var slot = new StorageSlotKey("0xContract", 42);
            var profile1 = new UserOpStorageProfile { UserOpHash = "0xA", SenderAddress = "0xSender1" };
            profile1.WriteSlots.Add(slot);

            var profile2 = new UserOpStorageProfile { UserOpHash = "0xB", SenderAddress = "0xSender2" };
            profile2.WriteSlots.Add(slot);

            var profiles = new List<UserOpStorageProfile> { profile1, profile2 };

            // WHEN: Detecting conflicts
            var conflicts = detector.DetectConflicts(profiles);

            // THEN: Write-write conflict detected
            Assert.Contains(conflicts, c => c.Type == StorageConflictType.WriteWrite);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "BundleConflict")]
        public void Given_TwoUserOps_When_ReadWriteConflict_Then_DetectsConflict()
        {
            // SPEC: Read-write conflicts affect bundle atomicity
            var detector = new BundleStorageConflictDetector();

            var slot = new StorageSlotKey("0xContract", 42);
            var profile1 = new UserOpStorageProfile { UserOpHash = "0xA", SenderAddress = "0xSender1" };
            profile1.ReadSlots.Add(slot);

            var profile2 = new UserOpStorageProfile { UserOpHash = "0xB", SenderAddress = "0xSender2" };
            profile2.WriteSlots.Add(slot);

            var profiles = new List<UserOpStorageProfile> { profile1, profile2 };

            // WHEN: Detecting conflicts
            var conflicts = detector.DetectConflicts(profiles);

            // THEN: Read-write conflict detected
            Assert.Contains(conflicts, c => c.Type == StorageConflictType.ReadWrite);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "BundleConflict")]
        public void Given_TwoUserOps_When_SharedPaymasterWriteConflict_Then_DetectsEntityConflict()
        {
            // SPEC: Shared paymaster write conflicts are detected
            var detector = new BundleStorageConflictDetector();

            var paymasterAddress = "0xPaymaster";
            var slot = new StorageSlotKey(paymasterAddress, 100);

            var profile1 = new UserOpStorageProfile
            {
                UserOpHash = "0xA",
                SenderAddress = "0xSender1",
                Paymaster = paymasterAddress
            };
            profile1.WriteSlots.Add(slot);

            var profile2 = new UserOpStorageProfile
            {
                UserOpHash = "0xB",
                SenderAddress = "0xSender2",
                Paymaster = paymasterAddress
            };
            profile2.WriteSlots.Add(slot);

            var profiles = new List<UserOpStorageProfile> { profile1, profile2 };

            // WHEN: Detecting conflicts
            var conflicts = detector.DetectConflicts(profiles);

            // THEN: Entity conflict detected for shared paymaster
            Assert.Contains(conflicts, c => c.Type == StorageConflictType.EntityConflict);
        }

        #endregion

        #region Associated Storage Calculation

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "AssociatedStorage")]
        public void Given_MappingSlot_When_ContainsSenderAddress_Then_IsAssociated()
        {
            // SPEC: Slot is associated if keccak(A||x) where A is sender address
            var calculator = new AssociatedStorageCalculator();
            var senderAddress = "0x1234567890123456789012345678901234567890";

            // Register slots for the sender
            calculator.RegisterSenderSlot(senderAddress, 0);

            // The registered slots should be associated
            // Note: The exact slot values depend on keccak calculation
            var associatedSlots = calculator.GetAssociatedSlots();
            Assert.NotEmpty(associatedSlots);
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Feature", "AssociatedStorage")]
        public void Given_KeccakPreimage_When_ContainsAddress_Then_TracksAssociation()
        {
            // SPEC: Track keccak inputs that contain addresses for association
            var calculator = new AssociatedStorageCalculator();
            var senderAddress = "0x1234567890123456789012345678901234567890";

            // Simulate a keccak operation with address in preimage (mapping key)
            var preimage = new byte[64];
            var addressBytes = senderAddress.HexToByteArray();
            Array.Copy(addressBytes, 0, preimage, 12, 20); // Address padded to 32 bytes
            // Second 32 bytes would be the mapping slot

            var resultHash = BigInteger.Parse("12345678901234567890");
            calculator.TrackKeccakFromHash(preimage, resultHash);

            // The result should be tracked as potentially associated
            Assert.True(calculator.IsAssociatedSlot("0xContract", resultHash, senderAddress));
        }

        #endregion

        #region Helper Methods

        private static ERC7562ValidationContext CreateValidationContext()
        {
            return new ERC7562ValidationContext
            {
                EntryPointAddress = "0x5ff137d4b0fdcd49dca30c7cf57e578a026d2789",
                Sender = new EntityInfo { Address = "0xSender", IsStaked = false },
                CurrentEntity = EntityType.Sender,
                StrictMode = true
            };
        }

        #endregion
    }

    /// <summary>
    /// Tests for forbidden opcodes helper class
    /// </summary>
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
        [InlineData(0x01, true)]   // ecrecover
        [InlineData(0x02, true)]   // sha256
        [InlineData(0x09, true)]   // blake2f
        [InlineData(0x0a, true)]   // kzg
        [InlineData(0xFF, false)]  // unknown
        [InlineData(0x20, false)]  // unknown
        public void Given_PrecompileAddress_Then_IsAllowedPrecompileReturnsCorrectly(int address, bool expected)
        {
            Assert.Equal(expected, ForbiddenOpcodes.IsAllowedPrecompile(address, includeRip7212: false));
        }

        [Fact]
        [Trait("Category", "ERC7562")]
        [Trait("Rule", "OP-062")]
        public void Given_RIP7212Precompile_When_Allowed_Then_ReturnsTrue()
        {
            // RIP-7212 secp256r1 is at address 0x100
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
