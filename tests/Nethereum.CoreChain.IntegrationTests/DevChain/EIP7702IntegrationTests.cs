using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit;

namespace Nethereum.CoreChain.IntegrationTests.DevChain
{
    public class EIP7702IntegrationTests : IClassFixture<EIP7702TestFixture>
    {
        private readonly EIP7702TestFixture _fixture;

        public EIP7702IntegrationTests(EIP7702TestFixture fixture)
        {
            _fixture = fixture;
        }

        [Fact]
        [Trait("Category", "EIP7702-DevChain")]
        public async Task Type4Transaction_SetsAuthorizationCode_OnEOA()
        {
            // GIVEN: A fresh authority EOA that will delegate to a contract
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAuthority();
            await _fixture.FundAddressAsync(authorityAddress, _fixture.InitialBalance);
            var delegateAddress = "0x1111111111111111111111111111111111111111";

            // Create and sign authorization (authority's nonce is 0 for new account)
            var signedAuth = _fixture.SignAuthorization(authorityKey, delegateAddress, 0);

            // Create Type 4 transaction
            var senderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var signedTx = _fixture.CreateAndSignType4Transaction(
                senderNonce,
                authorityAddress,
                new List<Authorisation7702Signed> { signedAuth });

            // WHEN: Transaction is sent
            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            // THEN: Transaction should succeed and delegation code should be set
            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");

            var code = await _fixture.Node.GetCodeAsync(authorityAddress);
            Assert.NotNull(code);
            Assert.Equal(23, code.Length);
            Assert.Equal(0xef, code[0]);
            Assert.Equal(0x01, code[1]);
            Assert.Equal(0x00, code[2]);

            var extractedAddress = new byte[20];
            Array.Copy(code, 3, extractedAddress, 0, 20);
            Assert.Equal(delegateAddress.ToLowerInvariant(), ("0x" + extractedAddress.ToHex()).ToLowerInvariant());
        }

        [Fact]
        [Trait("Category", "EIP7702-DevChain")]
        public async Task Type4Transaction_ExecutesDelegateCode_WhenCallingDelegatedEOA()
        {
            // GIVEN: A fresh authority and delegate contract that returns 0x42
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAuthority();
            await _fixture.FundAddressAsync(authorityAddress, _fixture.InitialBalance);

            // Deploy delegate contract: PUSH1 0x42 PUSH1 0x00 MSTORE PUSH1 0x20 PUSH1 0x00 RETURN
            var delegateCode = "604260005260206000F3".HexToByteArray();
            var delegateAddress = "0x2222222222222222222222222222222222222222";
            await _fixture.Node.SetCodeAsync(delegateAddress, delegateCode);

            // Create authorization (nonce = 0 for fresh account)
            var signedAuth = _fixture.SignAuthorization(authorityKey, delegateAddress, 0);

            var senderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var signedTx = _fixture.CreateAndSignType4Transaction(
                senderNonce,
                authorityAddress,
                new List<Authorisation7702Signed> { signedAuth });

            // WHEN: Transaction is sent
            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            // THEN: Transaction should succeed with return data from delegate
            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");
            Assert.NotNull(result.ReturnData);
            Assert.True(result.ReturnData.Length >= 32, $"Return data too short: {result.ReturnData.Length}");

            var returnValue = new BigInteger(result.ReturnData, true, true);
            Assert.Equal(0x42, returnValue);
        }

        [Fact]
        [Trait("Category", "EIP7702-DevChain")]
        public async Task Type4Transaction_IncrementsAuthorityNonce_OnSuccessfulAuthorization()
        {
            // GIVEN: A fresh authority with known nonce
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAuthority();
            await _fixture.FundAddressAsync(authorityAddress, _fixture.InitialBalance);
            var delegateAddress = "0x3333333333333333333333333333333333333333";

            var initialNonce = await _fixture.Node.GetNonceAsync(authorityAddress);
            Assert.Equal(BigInteger.Zero, initialNonce);

            var signedAuth = _fixture.SignAuthorization(authorityKey, delegateAddress, initialNonce);

            var senderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var signedTx = _fixture.CreateAndSignType4Transaction(
                senderNonce,
                authorityAddress,
                new List<Authorisation7702Signed> { signedAuth });

            // WHEN: Transaction is sent
            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            // THEN: Authority nonce should be incremented
            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");

            var finalNonce = await _fixture.Node.GetNonceAsync(authorityAddress);
            Assert.Equal(initialNonce + 1, finalNonce);
        }

        [Fact]
        [Trait("Category", "EIP7702-DevChain")]
        public async Task Type4Transaction_SkipsAuthorization_WhenNonceMismatch()
        {
            // GIVEN: A fresh authority with authorization using WRONG nonce
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAuthority();
            await _fixture.FundAddressAsync(authorityAddress, _fixture.InitialBalance);
            var delegateAddress = "0x4444444444444444444444444444444444444444";

            // Create authorization with WRONG nonce (100 instead of 0)
            var signedAuth = _fixture.SignAuthorization(authorityKey, delegateAddress, 100);

            var senderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var signedTx = _fixture.CreateAndSignType4Transaction(
                senderNonce,
                authorityAddress,
                new List<Authorisation7702Signed> { signedAuth });

            // WHEN: Transaction is sent
            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            // THEN: Transaction should succeed but no delegation code should be set
            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");

            var code = await _fixture.Node.GetCodeAsync(authorityAddress);
            Assert.True(code == null || code.Length == 0 || code[0] != 0xef,
                "Delegation code should NOT be set when nonce mismatches");
        }

        [Fact]
        [Trait("Category", "EIP7702-DevChain")]
        public async Task Type4Transaction_ClearsDelegation_WhenZeroAddressUsed()
        {
            // GIVEN: A fresh authority with delegation that will be cleared
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAuthority();
            await _fixture.FundAddressAsync(authorityAddress, _fixture.InitialBalance);
            var delegateAddress = "0x5555555555555555555555555555555555555555";

            // First set up delegation (nonce = 0)
            var setupAuth = _fixture.SignAuthorization(authorityKey, delegateAddress, 0);

            var senderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var setupTx = _fixture.CreateAndSignType4Transaction(
                senderNonce,
                authorityAddress,
                new List<Authorisation7702Signed> { setupAuth });

            var setupResult = await _fixture.Node.SendTransactionAsync(setupTx);
            Assert.True(setupResult.Success, $"Setup transaction failed: {setupResult.RevertReason}");

            // Verify delegation is set
            var codeAfterSetup = await _fixture.Node.GetCodeAsync(authorityAddress);
            Assert.Equal(23, codeAfterSetup.Length);

            // NOW: Create authorization to remove delegation (zero address, nonce = 1 now)
            var clearAuth = _fixture.SignAuthorization(
                authorityKey,
                "0x0000000000000000000000000000000000000000",
                1);

            var newSenderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var clearTx = _fixture.CreateAndSignType4Transaction(
                newSenderNonce,
                authorityAddress,
                new List<Authorisation7702Signed> { clearAuth });

            // WHEN: Clear transaction is sent
            var clearResult = await _fixture.Node.SendTransactionAsync(clearTx);

            // THEN: Delegation should be cleared
            Assert.True(clearResult.Success, $"Clear transaction failed: {clearResult.RevertReason}");

            var codeAfterClear = await _fixture.Node.GetCodeAsync(authorityAddress);
            Assert.True(codeAfterClear == null || codeAfterClear.Length == 0,
                $"Delegation code should be cleared, but got {codeAfterClear?.Length ?? 0} bytes");
        }

        [Fact]
        [Trait("Category", "EIP7702-DevChain")]
        public async Task Type4Transaction_GasIncludesAuthorizationCost()
        {
            // GIVEN: Two fresh authorities for authorization cost test
            var (authority1Key, authority1Address) = _fixture.GenerateNewAuthority();
            var (authority2Key, authority2Address) = _fixture.GenerateNewAuthority();
            await _fixture.FundAddressAsync(authority1Address, _fixture.InitialBalance);
            await _fixture.FundAddressAsync(authority2Address, _fixture.InitialBalance);
            var delegateAddress = "0x6666666666666666666666666666666666666666";

            var auth1 = _fixture.SignAuthorization(authority1Key, delegateAddress, 0);
            var auth2 = _fixture.SignAuthorization(authority2Key, delegateAddress, 0);

            // Send to a random recipient (not the authorities)
            var recipientAddress = "0x7777777777777777777777777777777777777777";

            var senderNonce = await _fixture.Node.GetNonceAsync(_fixture.SenderAddress);
            var signedTx = _fixture.CreateAndSignType4Transaction(
                senderNonce,
                recipientAddress,
                new List<Authorisation7702Signed> { auth1, auth2 });

            // WHEN: Transaction is sent
            var result = await _fixture.Node.SendTransactionAsync(signedTx);

            // THEN: Gas should include authorization costs
            // Base: 21000 + 2 * 12500 (PER_AUTH_BASE_COST) = 46000
            Assert.True(result.Success, $"Transaction failed: {result.RevertReason}");
            Assert.True(result.GasUsed >= 46000,
                $"Expected gas >= 46000 (21000 + 2*12500), got {result.GasUsed}");
        }
    }
}
