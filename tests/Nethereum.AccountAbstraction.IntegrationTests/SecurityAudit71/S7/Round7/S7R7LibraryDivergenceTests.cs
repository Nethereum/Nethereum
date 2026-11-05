using Nethereum.DevChain;
using System.Numerics;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round3;
using Nethereum.Hex.HexConvertors.Extensions;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.SecurityAudit71.S7.Round7
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "DevChainBundler")]
    public class S7R7LibraryDivergenceTests
    {
        private const string PaymasterSignatureMagic = "22e325a297439656";

        private readonly DevChainBundlerFixture _fixture;
        private readonly S7R3Support _support;

        public S7R7LibraryDivergenceTests(DevChainBundlerFixture fixture)
        {
            _fixture = fixture;
            _support = new S7R3Support(fixture);
        }

        [Fact]
        [Trait("Rule", "S7-R7-04")]
        [Trait("Erc", "UserOperationLib.paymasterDataKeccak")]
        public async Task PackAndHashEIP712UserOperation_WhenThePaymasterDataEndsWithAPaymasterSignature_EqualsTheEntryPointUserOpHash()
        {
            var signature = new byte[65];
            Array.Fill(signature, (byte)0x11);
            var paymasterData = new byte[] { 0xde, 0xad, 0xbe, 0xef }
                .Concat(signature)
                .Concat(new byte[] { 0x00, 0x41 })
                .Concat(PaymasterSignatureMagic.HexToByteArray())
                .ToArray();
            var (account, _) = await _support.AccountAsync();
            var userOp = new UserOperation
            {
                Sender = account,
                Nonce = 0,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                CallGasLimit = 100_000,
                VerificationGasLimit = 300_000,
                PreVerificationGas = 60_000,
                MaxFeePerGas = 2_000_000_000,
                MaxPriorityFeePerGas = 1_000_000_000,
                Paymaster = "0x2222222222222222222222222222222222222222",
                PaymasterData = paymasterData,
                PaymasterVerificationGasLimit = 100_000,
                PaymasterPostOpGasLimit = 100_000,
                Signature = new byte[65]
            };

            var libraryHash = UserOperationBuilder.PackAndHashEIP712UserOperation(
                userOp, _fixture.EntryPointService.ContractAddress, DevChainBundlerFixture.CHAIN_ID);
            var entryPointHash = await _fixture.EntryPointService.GetUserOpHashQueryAsync(
                UserOperationBuilder.PackUserOperation(userOp));

            Assert.Equal(entryPointHash.ToHex(true), libraryHash.ToHex(true));
        }

        [Fact]
        [Trait("Rule", "S7-R7-05")]
        [Trait("Erc", "EntryPoint.getNonce")]
        public async Task InitialiseUserOperationAsync_WhenTheOpIsEip7702AndNoNonceIsGiven_UsesTheEntryPointNonceNotTheAccountTransactionCount()
        {
            var sender = _fixture.OperatorAccount.Address;
            var transactionCount = (await _fixture.Web3.Eth.Transactions.GetTransactionCount.SendRequestAsync(sender)).Value;
            var entryPointNonce = await _fixture.EntryPointService.GetNonceQueryAsync(sender, BigInteger.Zero);
            Assert.True(transactionCount > entryPointNonce, "setup: the account must have sent transactions");
            var op = new UserOperation
            {
                Sender = sender,
                InitCode = AAEIP7702Utils.BuildInitCode("0x7702", Array.Empty<byte>()),
                CallData = Array.Empty<byte>()
            };

            var initialised = await _fixture.EntryPointService.InitialiseUserOperationAsync(
                op, "0x1111111111111111111111111111111111111111");

            Assert.Equal(entryPointNonce, initialised.Nonce);
        }

        [Fact]
        [Trait("Rule", "S7-R7-07")]
        [Trait("Erc", "ERC-4337 mempool sender balance")]
        public async Task SendUserOperation_WhenASecondOpOnAnotherNonceKeyCannotBePaidForByTheSender_IsRefusedLikeTheSameKeyOp()
        {
            var (account, key) = await _support.AccountAsync();
            var config = _support.NewConfig();
            using var bundler = new BundlerService(_fixture.Node.CreateWeb3(_fixture.BundlerAccount), config);
            var entryPoint = _fixture.EntryPointService.ContractAddress;
            var baseNonce = await _fixture.EntryPointService.GetNonceQueryAsync(account, BigInteger.Zero);
            var first = await _support.OpAsync(account, key, baseNonce, 1_000_000_000, 1_200_000_000_000);
            var sameKey = await _support.OpAsync(account, key, baseNonce + 1, 1_000_000_000, 1_200_000_000_000);
            var otherKey = await _support.OpAsync(account, key, BigInteger.One << 64, 1_000_000_000, 1_200_000_000_000);
            await bundler.SendUserOperationAsync(first, entryPoint);

            var sameKeyRefusal = await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(sameKey, entryPoint));
            Assert.Contains("AA21", sameKeyRefusal.Message);

            await Assert.ThrowsAsync<BundlerRpcException>(
                () => bundler.SendUserOperationAsync(otherKey, entryPoint));
        }
    }
}
