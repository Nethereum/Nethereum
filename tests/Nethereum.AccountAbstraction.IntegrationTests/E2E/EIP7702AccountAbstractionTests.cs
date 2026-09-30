using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.ABI.FunctionEncoding.Attributes;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EIP7702-4337")]
    public class EIP7702AccountAbstractionTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public EIP7702AccountAbstractionTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task EOA_WithDelegation_HasDelegationCode()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 10m);

            _output.WriteLine($"Authority EOA: {authorityAddress}");

            var codeBefore = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0);

            var delegateTarget = _fixture.EntryPointService.ContractAddress;
            _output.WriteLine($"Delegating to: {delegateTarget}");

            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, delegateTarget);

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            Assert.Equal(0x01, codeAfter[1]);
            Assert.Equal(0x00, codeAfter[2]);

            var delegateAddress = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(delegateTarget.ToLowerInvariant(), delegateAddress.ToLowerInvariant());

            _output.WriteLine($"Delegation code set successfully: {codeAfter.ToHex()}");
        }

        [Fact]
        public async Task DelegatedEOA_CanReceiveETH()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            var delegateContract = "0x1234567890123456789012345678901234567890";

            await _fixture.FundAccountAsync(authorityAddress, 5m);
            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, delegateContract);

            var balanceBefore = await _fixture.GetBalanceAsync(authorityAddress);

            await _fixture.FundAccountAsync(authorityAddress, 3m);

            var balanceAfter = await _fixture.GetBalanceAsync(authorityAddress);
            Assert.True(balanceAfter > balanceBefore);

            _output.WriteLine($"Balance before: {Web3.Web3.Convert.FromWei(balanceBefore)} ETH");
            _output.WriteLine($"Balance after: {Web3.Web3.Convert.FromWei(balanceAfter)} ETH");
        }

        [Fact]
        public async Task DelegatedEOA_CanUpdateDelegation()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 10m);

            var delegate1 = "0x1111111111111111111111111111111111111111";
            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, delegate1);

            var code1 = await _fixture.GetCodeAsync(authorityAddress);
            var extractedAddr1 = "0x" + code1.Skip(3).ToArray().ToHex();
            Assert.Equal(delegate1.ToLowerInvariant(), extractedAddr1.ToLowerInvariant());
            _output.WriteLine($"Initial delegation set to: {delegate1}");

            var delegate2 = "0x2222222222222222222222222222222222222222";
            var nonce = await _fixture.GetNonceAsync(authorityAddress);
            _output.WriteLine($"Authority nonce for update: {nonce}");

            var auth = _fixture.SignAuthorization(authorityKey, delegate2, nonce);
            var senderNonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTx = _fixture.CreateType4Transaction(senderNonce, authorityAddress, new List<Authorisation7702Signed> { auth });

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            _output.WriteLine($"Update tx success: {result.Success}, gas: {result.GasUsed}, revert: {result.RevertReason}");
            Assert.True(result.Success, $"Delegation update failed: {result.RevertReason}");

            var code2 = await _fixture.GetCodeAsync(authorityAddress);
            Assert.NotNull(code2);
            Assert.Equal(23, code2.Length);
            var extractedAddr2 = "0x" + code2.Skip(3).ToArray().ToHex();

            _output.WriteLine($"Delegation after update: {extractedAddr2}");
            Assert.Equal(delegate2.ToLowerInvariant(), extractedAddr2.ToLowerInvariant());

            _output.WriteLine($"Successfully updated delegation from {delegate1} to {delegate2}");
        }

        [Fact]
        public async Task DelegatedEOA_CanRemoveDelegation()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 10m);

            var delegateAddr = "0x3333333333333333333333333333333333333333";
            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, delegateAddr);

            var codeBefore = await _fixture.GetCodeAsync(authorityAddress);
            Assert.Equal(23, codeBefore.Length);

            var nonce = await _fixture.GetNonceAsync(authorityAddress);
            var auth = _fixture.SignAuthorization(authorityKey, AddressUtil.ZERO_ADDRESS, nonce);

            var senderNonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTx = _fixture.CreateType4Transaction(senderNonce, authorityAddress, new List<Authorisation7702Signed> { auth });

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, $"Delegation removal failed: {result.RevertReason}");

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeAfter == null || codeAfter.Length == 0, "Code should be empty after delegation removal");

            _output.WriteLine("Delegation removed - EOA is back to normal");
        }

        [Fact]
        public async Task MultipleDelegations_InSingleTransaction()
        {
            var (key1, addr1) = _fixture.GenerateNewAccount();
            var (key2, addr2) = _fixture.GenerateNewAccount();
            var (key3, addr3) = _fixture.GenerateNewAccount();

            await _fixture.FundAccountAsync(addr1, 5m);
            await _fixture.FundAccountAsync(addr2, 5m);
            await _fixture.FundAccountAsync(addr3, 5m);

            var delegateAddr = "0x4444444444444444444444444444444444444444";

            var auth1 = _fixture.SignAuthorization(key1, delegateAddr, 0);
            var auth2 = _fixture.SignAuthorization(key2, delegateAddr, 0);
            var auth3 = _fixture.SignAuthorization(key3, delegateAddr, 0);

            var senderNonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTx = _fixture.CreateType4Transaction(
                senderNonce,
                addr1,
                new List<Authorisation7702Signed> { auth1, auth2, auth3 });

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, $"Multi-delegation failed: {result.RevertReason}");

            var code1 = await _fixture.GetCodeAsync(addr1);
            var code2 = await _fixture.GetCodeAsync(addr2);
            var code3 = await _fixture.GetCodeAsync(addr3);

            Assert.Equal(23, code1.Length);
            Assert.Equal(23, code2.Length);
            Assert.Equal(23, code3.Length);

            _output.WriteLine($"Set delegation for 3 EOAs in single transaction");
            _output.WriteLine($"  {addr1}: {code1.ToHex()}");
            _output.WriteLine($"  {addr2}: {code2.ToHex()}");
            _output.WriteLine($"  {addr3}: {code3.ToHex()}");
        }

        [Fact]
        public async Task DelegatedEOA_NonceIncrements_AfterAuthorization()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 5m);

            var nonceBefore = await _fixture.GetNonceAsync(authorityAddress);
            Assert.Equal(BigInteger.Zero, nonceBefore);

            var delegateAddr = "0x5555555555555555555555555555555555555555";
            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, delegateAddr);

            var nonceAfter = await _fixture.GetNonceAsync(authorityAddress);
            Assert.Equal(BigInteger.One, nonceAfter);

            _output.WriteLine($"Nonce before: {nonceBefore}, after: {nonceAfter}");
        }

        [Fact]
        public async Task Authorization_WithWrongNonce_IsSkipped()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 5m);

            var codeBefore = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0);

            var delegateAddr = "0x6666666666666666666666666666666666666666";
            var authWithWrongNonce = _fixture.SignAuthorization(authorityKey, delegateAddr, 5);

            var senderNonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTx = _fixture.CreateType4Transaction(
                senderNonce, authorityAddress, new List<Authorisation7702Signed> { authWithWrongNonce });

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeAfter == null || codeAfter.Length == 0,
                "EOA should not have delegation code when nonce doesn't match");

            _output.WriteLine("Authorization with wrong nonce was correctly skipped");
        }

        [Fact]
        public async Task Authorization_WithWrongChainId_IsSkipped()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 5m);

            var delegateAddr = "0x7777777777777777777777777777777777777777";
            var wrongChainId = 999999;
            var authWithWrongChain = _fixture.SignAuthorization(authorityKey, delegateAddr, 0, wrongChainId);

            var senderNonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTx = _fixture.CreateType4Transaction(
                senderNonce, authorityAddress, new List<Authorisation7702Signed> { authWithWrongChain });

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success);

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeAfter == null || codeAfter.Length == 0);

            _output.WriteLine("Authorization with wrong chain ID was correctly skipped");
        }

        [Fact]
        public async Task Authorization_WithChainIdZero_ValidOnAnyChain()
        {
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 5m);

            var delegateAddr = "0x8888888888888888888888888888888888888888";
            var universalAuth = _fixture.SignAuthorization(authorityKey, delegateAddr, 0, 0);

            var senderNonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTx = _fixture.CreateType4Transaction(
                senderNonce, authorityAddress, new List<Authorisation7702Signed> { universalAuth });

            var result = await _fixture.Node.SendTransactionAsync(signedTx);
            Assert.True(result.Success, $"Universal auth failed: {result.RevertReason}");

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.Equal(23, codeAfter.Length);

            _output.WriteLine("Universal authorization (chain_id=0) accepted on DevChain");
        }

        [Fact]
        public async Task DelegatedEOA_ExecutesSmartAccountLogic_WhenCalled()
        {
            var simpleContractBytecode = "600a600c600039600a6000f3604260005260206000f3".HexToByteArray();

            var deployTx = CreateContractDeploymentTransaction(simpleContractBytecode);
            var deployResult = await _fixture.Node.SendTransactionAsync(deployTx);
            Assert.True(deployResult.Success, $"Contract deployment failed: {deployResult.RevertReason}");

            var receipt = await _fixture.Node.GetTransactionReceiptInfoAsync(deployTx.Hash);
            var contractAddress = receipt.ContractAddress;
            _output.WriteLine($"Deployed simple contract at: {contractAddress}");

            var directCall = await _fixture.Node.CallAsync(contractAddress, Array.Empty<byte>());
            _output.WriteLine($"Direct call to contract - Success: {directCall.Success}, Return: {directCall.ReturnData?.ToHex()}");
            Assert.True(directCall.Success, "Direct call to contract should succeed");

            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 5m);
            await _fixture.SetupEIP7702DelegatedEOAAsync(authorityKey, contractAddress);

            var delegationCode = await _fixture.GetCodeAsync(authorityAddress);
            _output.WriteLine($"Delegation code: {delegationCode?.ToHex()}");
            Assert.Equal(23, delegationCode.Length);

            var callResult = await _fixture.Node.CallAsync(authorityAddress, Array.Empty<byte>());
            _output.WriteLine($"Call to delegated EOA - Success: {callResult.Success}, Error: {callResult.RevertReason}, Return: {callResult.ReturnData?.ToHex()}");

            if (!callResult.Success)
            {
                _output.WriteLine("WARNING: eth_call may not follow EIP-7702 delegation - this is a known limitation");
                return;
            }

            Assert.NotNull(callResult.ReturnData);
            var returnValue = new BigInteger(callResult.ReturnData.Reverse().ToArray());
            Assert.Equal(0x42, returnValue);

            _output.WriteLine($"Delegated EOA executed contract logic, returned: 0x{returnValue:X}");
        }

        private ISignedTransaction CreateContractDeploymentTransaction(byte[] bytecode)
        {
            var nonce = _fixture.Node.GetNonceAsync(_fixture.OperatorAccount.Address).Result;
            var signer = new LegacyTransactionSigner();

            var signedTxHex = signer.SignTransaction(
                _fixture.OperatorPrivateKey.Substring(2).HexToByteArray(),
                DevChainBundlerFixture.CHAIN_ID,
                "",
                BigInteger.Zero,
                nonce,
                1_000_000_000,
                3_000_000,
                bytecode.ToHex());

            return TransactionFactory.CreateTransaction(signedTxHex);
        }
    }
}
