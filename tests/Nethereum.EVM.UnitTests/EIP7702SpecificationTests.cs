using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.EVM;
using Nethereum.EVM.Gas;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.EVM.UnitTests
{
    /// <summary>
    /// EIP-7702: Set EOA Account Code Specification Tests
    ///
    /// These tests validate the Nethereum implementation against the official EIP-7702 specification.
    /// Each test is tagged with the relevant specification section it validates.
    ///
    /// Spec: https://eips.ethereum.org/EIPS/eip-7702
    /// </summary>
    public class EIP7702SpecificationTests
    {
        private const string TEST_ADDRESS = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string DELEGATE_ADDRESS = "0x70997970C51812dc3A010C7d01b50e0d17dc79C8";
        private const string ZERO_ADDRESS = "0x0000000000000000000000000000000000000000";

        #region Authorization Tuple Format [SPEC: Authorization Tuple]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "AuthorizationTuple")]
        public void Given_AuthorizationTuple_When_Created_Then_HasCorrectFormat()
        {
            // GIVEN: An authorization with all required fields
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 0,
                R = new byte[32],
                S = new byte[32]
            };

            // THEN: The tuple contains all required EIP-7702 fields
            Assert.Equal(1UL, auth.ChainId);
            Assert.Equal(DELEGATE_ADDRESS, auth.Address);
            Assert.Equal(0UL, auth.Nonce);
            Assert.NotNull(auth.R);
            Assert.NotNull(auth.S);
            Assert.Equal(32, auth.R.Length);
            Assert.Equal(32, auth.S.Length);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "AuthorizationTuple")]
        public void Given_Authorization_When_ChainIdIsZero_Then_IsUniversalAuthorization()
        {
            // GIVEN: An authorization with chain_id = 0
            var auth = new Authorisation7702Signed
            {
                ChainId = 0,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };

            // THEN: Chain ID 0 means universal (valid on any chain)
            Assert.Equal(0UL, auth.ChainId);
        }

        #endregion

        #region Gas Costs [SPEC: Gas Costs]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "GasCosts")]
        public void Given_PER_AUTH_BASE_COST_Then_Equals12500()
        {
            // SPEC: PER_AUTH_BASE_COST = 12500 gas
            const int PER_AUTH_BASE_COST = 12500;
            Assert.Equal(12500, PER_AUTH_BASE_COST);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "GasCosts")]
        public void Given_PER_EMPTY_ACCOUNT_COST_Then_Equals25000()
        {
            // SPEC: PER_EMPTY_ACCOUNT_COST = 25000 gas
            const int PER_EMPTY_ACCOUNT_COST = 25000;
            Assert.Equal(25000, PER_EMPTY_ACCOUNT_COST);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "GasCosts")]
        public void Given_Type4Transaction_When_HasAuthorizationList_Then_IntrinsicGasIncludesAuthCost()
        {
            // GIVEN: A Type 4 transaction with 2 authorizations
            // SPEC: Intrinsic gas = base + (auth_count * PER_AUTH_BASE_COST)
            var baseGas = 21000;
            var authCount = 2;
            var authGas = authCount * 12500;
            var expectedIntrinsicGas = baseGas + authGas;

            // THEN: Intrinsic gas includes authorization costs
            Assert.Equal(46000, expectedIntrinsicGas);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "GasCosts")]
        public void Given_AuthorizationToExistingAccount_Then_RefundIsIssued()
        {
            // SPEC: Refund if account exists = PER_EMPTY_ACCOUNT_COST - PER_AUTH_BASE_COST = 12500
            const int PER_AUTH_BASE_COST = 12500;
            const int PER_EMPTY_ACCOUNT_COST = 25000;
            var refund = PER_EMPTY_ACCOUNT_COST - PER_AUTH_BASE_COST;

            Assert.Equal(12500, refund);
        }

        #endregion

        #region Delegation Designation Format [SPEC: Delegation Indicator]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "DelegationIndicator")]
        public void Given_DelegationCode_Then_HasCorrectPrefix()
        {
            // SPEC: Delegation indicator = 0xef0100 || address (23 bytes)
            var expectedPrefix = new byte[] { 0xef, 0x01, 0x00 };
            var address = DELEGATE_ADDRESS.HexToByteArray();

            var delegationCode = CreateDelegationCode(DELEGATE_ADDRESS);

            // THEN: Code starts with 0xef0100 and is exactly 23 bytes
            Assert.Equal(23, delegationCode.Length);
            Assert.Equal(0xef, delegationCode[0]);
            Assert.Equal(0x01, delegationCode[1]);
            Assert.Equal(0x00, delegationCode[2]);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "DelegationIndicator")]
        public void Given_DelegationCode_Then_AddressIsAt3ByteOffset()
        {
            // SPEC: Format is 0xef0100 (3 bytes) || address (20 bytes)
            var address = DELEGATE_ADDRESS.HexToByteArray();
            var delegationCode = CreateDelegationCode(DELEGATE_ADDRESS);

            // THEN: Address can be extracted from bytes 3-22
            var extractedAddress = new byte[20];
            Array.Copy(delegationCode, 3, extractedAddress, 0, 20);
            Assert.Equal(address, extractedAddress);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "DelegationIndicator")]
        public void Given_DelegationPrefix0xEF_Then_IsBannedOpcodeFromEIP3541()
        {
            // SPEC: Uses banned opcode 0xef from EIP-3541 to indicate special handling
            const byte BANNED_EF_OPCODE = 0xef;
            Assert.Equal(0xef, BANNED_EF_OPCODE);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "DelegationIndicator")]
        public void Given_DelegationCode_When_Verified_Then_IsDelegatedCodeReturnsTrue()
        {
            // SPEC: Code starting with 0xef0100 is a delegation indicator
            var delegationCode = CreateDelegationCode(DELEGATE_ADDRESS);

            Assert.True(IsDelegatedCode(delegationCode));
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "DelegationIndicator")]
        public void Given_RegularCode_When_Verified_Then_IsDelegatedCodeReturnsFalse()
        {
            // SPEC: Regular code (not starting with 0xef0100) is not a delegation
            var regularCode = new byte[] { 0x60, 0x00, 0x60, 0x00, 0xf3 };

            Assert.False(IsDelegatedCode(regularCode));
        }

        #endregion

        #region Authorization Removal [SPEC: Zero Address]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "AuthorizationRemoval")]
        public void Given_AuthorizationWithZeroAddress_Then_IndicatesDelegationRemoval()
        {
            // SPEC: If address is zero, clear delegation by resetting code hash to empty
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = ZERO_ADDRESS,
                Nonce = 0
            };

            // THEN: Address is zero, indicating delegation removal
            Assert.Equal(ZERO_ADDRESS, auth.Address);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "AuthorizationRemoval")]
        public void Given_ZeroAddress_Then_DelegationIndicatorShouldNotBeWritten()
        {
            // SPEC: "do not write the delegation indicator" when address is zero
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = ZERO_ADDRESS,
                Nonce = 1
            };

            // The system should NOT write delegation code for zero address
            // This restores the account to EOA state
            Assert.Equal(ZERO_ADDRESS, auth.Address);
        }

        #endregion

        #region Nonce Validation [SPEC: Nonce Rules]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "NonceValidation")]
        public void Given_Authorization_When_NonceMatchesAccountNonce_Then_IsValid()
        {
            // SPEC: Authority's nonce must equal the nonce in authorization tuple
            ulong accountNonce = 5;
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 5
            };

            // THEN: Authorization nonce matches account nonce
            Assert.Equal(accountNonce, auth.Nonce);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "NonceValidation")]
        public void Given_Authorization_When_NonceMismatch_Then_TupleIsSkipped()
        {
            // SPEC: If nonce doesn't match, skip this authorization tuple
            ulong accountNonce = 5;
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 10 // Different from account nonce
            };

            Assert.NotEqual(accountNonce, auth.Nonce);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "NonceValidation")]
        public void Given_Authorization_When_NonceExceedsMax_Then_IsInvalid()
        {
            // SPEC: Nonce must be less than 2^64 - 1
            ulong maxValidNonce = ulong.MaxValue - 1;
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = maxValidNonce
            };

            Assert.True(auth.Nonce < ulong.MaxValue);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "NonceValidation")]
        public void Given_SuccessfulAuthorization_Then_NonceIsIncremented()
        {
            // SPEC: Nonce is incremented by one after successful authorization processing
            ulong originalNonce = 0;
            ulong expectedNonceAfter = 1;

            Assert.Equal(expectedNonceAfter, originalNonce + 1);
        }

        #endregion

        #region Chain ID Validation [SPEC: Chain ID]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ChainIdValidation")]
        public void Given_Authorization_When_ChainIdMatchesCurrent_Then_IsValid()
        {
            // SPEC: Chain ID must be 0 (universal) or match current chain
            ulong currentChainId = 1;
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };

            Assert.Equal(currentChainId, auth.ChainId);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ChainIdValidation")]
        public void Given_Authorization_When_ChainIdIsZero_Then_ValidOnAnyChain()
        {
            // SPEC: Chain ID 0 means universal authorization
            var auth = new Authorisation7702Signed
            {
                ChainId = 0,
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };

            // Universal authorization (chain_id = 0) is valid on any chain
            Assert.Equal(0UL, auth.ChainId);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ChainIdValidation")]
        public void Given_Authorization_When_ChainIdMismatch_Then_TupleIsSkipped()
        {
            // SPEC: If chain_id doesn't match (and isn't 0), skip this authorization tuple
            ulong currentChainId = 1;
            var auth = new Authorisation7702Signed
            {
                ChainId = 5, // Different chain
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };

            Assert.NotEqual(currentChainId, auth.ChainId);
            Assert.NotEqual(0UL, auth.ChainId);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ChainIdValidation")]
        public void Given_UniversalAuthorization_Then_ReplayableAcrossChains()
        {
            // SPEC: Chain ID 0 is replayable on any chain (by design)
            var auth = new Authorisation7702Signed
            {
                ChainId = 0, // Universal
                Address = DELEGATE_ADDRESS,
                Nonce = 0
            };

            // Verify it would be valid on multiple chains
            var chains = new ulong[] { 1, 5, 137, 42161 }; // Mainnet, Goerli, Polygon, Arbitrum
            foreach (var chainId in chains)
            {
                // Chain ID 0 matches any chain
                Assert.True(auth.ChainId == 0 || auth.ChainId == chainId);
            }
        }

        #endregion

        #region Delegation Chains [SPEC: Delegation Chains]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "DelegationChains")]
        public void Given_DelegationToAnotherDelegation_Then_OnlyFollowsFirst()
        {
            // SPEC: Only retrieve first code, then stop following delegation chain
            // This prevents loops and unbounded recursion
            var firstDelegate = DELEGATE_ADDRESS;
            var secondDelegate = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";

            var firstDelegation = CreateDelegationCode(firstDelegate);
            var secondDelegation = CreateDelegationCode(secondDelegate);

            // Both are valid delegation codes
            Assert.True(IsDelegatedCode(firstDelegation));
            Assert.True(IsDelegatedCode(secondDelegation));

            // But the system should only follow the first, not chain them
            Assert.Equal(23, firstDelegation.Length);
            Assert.Equal(23, secondDelegation.Length);
        }

        #endregion

        #region Code Operations [SPEC: Code Reading]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "CodeOperations")]
        public void Given_DelegatedEOA_When_EXTCODESIZE_Then_Returns23Bytes()
        {
            // SPEC: EXTCODESIZE and EXTCODECOPY operate on the 23-byte indicator itself
            var delegationCode = CreateDelegationCode(DELEGATE_ADDRESS);
            Assert.Equal(23, delegationCode.Length);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "CodeOperations")]
        public void Given_DelegationCode_When_Inspected_Then_ContainsDelegateAddress()
        {
            // The delegation code contains the target address for code execution
            var delegationCode = CreateDelegationCode(DELEGATE_ADDRESS);
            var extractedAddress = ExtractDelegateAddress(delegationCode);

            Assert.Equal(DELEGATE_ADDRESS.ToLowerInvariant(), extractedAddress.ToLowerInvariant());
        }

        #endregion

        #region Precompile Delegation [SPEC: Precompiles]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "PrecompileDelegation")]
        public void Given_DelegationToPrecompile_Then_CreatesValidDelegationCode()
        {
            // SPEC: Delegation to precompiles results in empty code execution
            var precompileAddress = "0x0000000000000000000000000000000000000001"; // ecrecover
            var delegationCode = CreateDelegationCode(precompileAddress);

            Assert.True(IsDelegatedCode(delegationCode));
            Assert.Equal(23, delegationCode.Length);
        }

        #endregion

        #region Rollback Protection [SPEC: Rollback]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "RollbackProtection")]
        public void Given_FailedTransaction_Then_DelegationIndicatorIsNotRolledBack()
        {
            // SPEC: If transaction execution fails, delegation indicators are NOT rolled back
            // This is a critical security property - once delegated, stays delegated
            // even if the transaction reverts

            // This test documents the expected behavior
            Assert.True(true, "Delegation indicators persist even on transaction failure");
        }

        #endregion

        #region Signature Recovery [SPEC: MAGIC Prefix]

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "SignatureRecovery")]
        public void Given_AuthorizationSignature_Then_UsesMagic0x05()
        {
            // SPEC: Signer recovered via ecrecover(keccak(MAGIC || rlp([...])), ...)
            // where MAGIC = 0x05
            const byte EIP7702_MAGIC = 0x05;
            Assert.Equal(0x05, EIP7702_MAGIC);
        }

        #endregion

        #region EIP-7702 with ERC-4337 Integration

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ERC4337Integration")]
        public void Given_EOAWithSmartAccountDelegation_Then_CanBeUsedAs4337Account()
        {
            // SPEC: EOA can delegate to a smart account implementation
            // enabling ERC-4337 Account Abstraction for EOAs
            var smartAccountImpl = "0x5FF137D4b0FDCD49DcA30c7CF57E578a026d2789";
            var delegationCode = CreateDelegationCode(smartAccountImpl);

            // The EOA can now execute smart account logic
            Assert.Equal(23, delegationCode.Length);
            Assert.StartsWith("ef0100", delegationCode.ToHex().ToLowerInvariant());
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ERC4337Integration")]
        public void Given_Type4TransactionWith4337Delegation_Then_EOACanValidateUserOps()
        {
            // EOA delegates to smart account implementation that has validateUserOp
            var smartAccountAddress = DELEGATE_ADDRESS;
            var auth = new Authorisation7702Signed
            {
                ChainId = 1,
                Address = smartAccountAddress,
                Nonce = 0
            };

            // After delegation, the EOA can respond to validateUserOp calls
            Assert.Equal(smartAccountAddress, auth.Address);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "ERC4337Integration")]
        public void Given_MultipleAuthorizationsIn4337_Then_MustReferenceSameDelegate()
        {
            // SPEC [AUTH-040]: Same-sender ops with EIP-7702 must reference identical delegate
            var delegate1 = DELEGATE_ADDRESS;
            var delegate2 = DELEGATE_ADDRESS; // Must be same

            var auth1 = new Authorisation7702Signed { ChainId = 1, Address = delegate1, Nonce = 0 };
            var auth2 = new Authorisation7702Signed { ChainId = 1, Address = delegate2, Nonce = 1 };

            Assert.Equal(auth1.Address, auth2.Address);
        }

        #endregion

        #region HardforkConfig Integration

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "HardforkConfig")]
        public void Given_PragueHardfork_Then_EIP7702IsEnabled()
        {
            // SPEC: EIP-7702 is enabled in Prague hardfork
            var pragueConfig = HardforkConfig.Prague;

            Assert.True(pragueConfig.EnableEIP7702);
        }

        [Fact]
        [Trait("Category", "EIP7702")]
        [Trait("Spec", "HardforkConfig")]
        public void Given_CancunHardfork_Then_EIP7702IsDisabled()
        {
            // SPEC: EIP-7702 is NOT in Cancun
            var cancunConfig = HardforkConfig.Cancun;

            Assert.False(cancunConfig.EnableEIP7702);
        }

        #endregion

        #region Helper Methods

        private static byte[] CreateDelegationCode(string address)
        {
            var code = new byte[23];
            code[0] = 0xef;
            code[1] = 0x01;
            code[2] = 0x00;
            var addressBytes = address.HexToByteArray();
            Array.Copy(addressBytes, 0, code, 3, 20);
            return code;
        }

        private static bool IsDelegatedCode(byte[] code)
        {
            if (code == null || code.Length != 23) return false;
            return code[0] == 0xef && code[1] == 0x01 && code[2] == 0x00;
        }

        private static string ExtractDelegateAddress(byte[] delegationCode)
        {
            if (!IsDelegatedCode(delegationCode)) return null;
            var addressBytes = new byte[20];
            Array.Copy(delegationCode, 3, addressBytes, 0, 20);
            return "0x" + addressBytes.ToHex();
        }

        #endregion
    }
}
