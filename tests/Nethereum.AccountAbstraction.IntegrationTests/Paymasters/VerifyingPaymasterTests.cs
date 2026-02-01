using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster;
using Nethereum.AccountAbstraction.Contracts.Paymaster.VerifyingPaymaster.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.Bundler;
using Nethereum.AccountAbstraction.SimpleAccount.SimpleAccount.ContractDefinition;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Signer;
using Nethereum.Web3;
using Nethereum.XUnitEthereumClients;
using System.Numerics;
using Xunit;

namespace Nethereum.AccountAbstraction.IntegrationTests.Paymasters
{
    [Collection(BundlerTestFixture.BUNDLER_COLLECTION)]
    public class VerifyingPaymasterTests
    {
        private readonly BundlerTestFixture _fixture;
        private VerifyingPaymasterService? _paymasterService;

        public VerifyingPaymasterTests(BundlerTestFixture fixture)
        {
            _fixture = fixture;
        }

        private async Task<VerifyingPaymasterService> GetOrDeployPaymasterAsync()
        {
            if (_paymasterService != null)
                return _paymasterService;

            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            _paymasterService = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            return _paymasterService;
        }

        [Fact]
        public async Task DeployPaymaster_WithValidParams_Succeeds()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var service = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            Assert.NotNull(service);
            Assert.NotEmpty(service.ContractAddress);
        }

        [Fact]
        public async Task GetEntryPoint_ReturnsConfiguredEntryPoint()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var entryPoint = await paymaster.EntryPointQueryAsync();

            Assert.Equal(
                _fixture.EntryPointService.ContractAddress.ToLower(),
                entryPoint.ToLower());
        }

        [Fact]
        public async Task GetOwner_ReturnsConfiguredOwner()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal(_fixture.BeneficiaryAddress.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task GetVerifyingSigner_ReturnsConfiguredSigner()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var signer = await paymaster.VerifyingSignerQueryAsync();

            Assert.Equal(_fixture.BeneficiaryAddress.ToLower(), signer.ToLower());
        }

        [Fact]
        public async Task GetDeposit_InitiallyZero()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var deposit = await paymaster.GetDepositQueryAsync();

            Assert.Equal(BigInteger.Zero, deposit);
        }

        [Fact]
        public async Task Deposit_IncreasesBalance()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var depositAmount = Web3.Web3.Convert.ToWei(0.1m);
            var depositFunction = new DepositFunction { AmountToSend = depositAmount };
            await paymaster.DepositRequestAndWaitForReceiptAsync(depositFunction);

            var deposit = await paymaster.GetDepositQueryAsync();

            Assert.Equal(depositAmount, deposit);
        }

        [Fact]
        public async Task SetVerifyingSigner_ByOwner_Updates()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var newSigner = "0x1234567890123456789012345678901234567890";
            await paymaster.SetVerifyingSignerRequestAndWaitForReceiptAsync(newSigner);

            var signer = await paymaster.VerifyingSignerQueryAsync();

            Assert.Equal(newSigner.ToLower(), signer.ToLower());
        }

        [Fact]
        public async Task TransferOwnership_ByOwner_TransfersOwnership()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var newOwner = "0x1234567890123456789012345678901234567890";
            await paymaster.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner);

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal(newOwner.ToLower(), owner.ToLower());
        }

        [Fact]
        public async Task GetHash_ReturnsValidHash()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var packedUserOp = new Structs.PackedUserOperation
            {
                Sender = accountAddress,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var hash = await paymaster.GetHashQueryAsync(packedUserOp, 0, 0);

            Assert.NotNull(hash);
            Assert.Equal(32, hash.Length);
            Assert.True(hash.Any(b => b != 0));
        }

        [Fact]
        public async Task GetHash_DifferentUserOps_ProduceDifferentHashes()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var salt1 = (ulong)Random.Shared.NextInt64();
            var salt2 = (ulong)Random.Shared.NextInt64();
            var (accountAddress1, _) = await _fixture.CreateFundedAccountAsync(salt1);
            var (accountAddress2, _) = await _fixture.CreateFundedAccountAsync(salt2);

            var packedUserOp1 = new Structs.PackedUserOperation
            {
                Sender = accountAddress1,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var packedUserOp2 = new Structs.PackedUserOperation
            {
                Sender = accountAddress2,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var hash1 = await paymaster.GetHashQueryAsync(packedUserOp1, 0, 0);
            var hash2 = await paymaster.GetHashQueryAsync(packedUserOp2, 0, 0);

            Assert.NotEqual(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task GetHash_DifferentValidityTimes_ProduceDifferentHashes()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var packedUserOp = new Structs.PackedUserOperation
            {
                Sender = accountAddress,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = new byte[32],
                PreVerificationGas = 21000,
                GasFees = new byte[32],
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };

            var hash1 = await paymaster.GetHashQueryAsync(packedUserOp, 1000, 0);
            var hash2 = await paymaster.GetHashQueryAsync(packedUserOp, 2000, 0);

            Assert.NotEqual(hash1.ToHex(), hash2.ToHex());
        }

        [Fact]
        public async Task SenderNonce_InitiallyZero()
        {
            var paymaster = await GetOrDeployPaymasterAsync();

            var salt = (ulong)Random.Shared.NextInt64();
            var (accountAddress, _) = await _fixture.CreateFundedAccountAsync(salt);

            var nonce = await paymaster.SenderNonceQueryAsync(accountAddress);

            Assert.Equal(BigInteger.Zero, nonce);
        }

        [Fact]
        public async Task WithdrawTo_ByOwner_WithdrawsFunds()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var depositAmount = Web3.Web3.Convert.ToWei(0.1m);
            var depositFunction = new DepositFunction { AmountToSend = depositAmount };
            await paymaster.DepositRequestAndWaitForReceiptAsync(depositFunction);

            var withdrawAmount = Web3.Web3.Convert.ToWei(0.05m);
            var withdrawTo = "0x1111111111111111111111111111111111111111";

            var balanceBefore = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(withdrawTo);
            await paymaster.WithdrawToRequestAndWaitForReceiptAsync(withdrawTo, withdrawAmount);
            var balanceAfter = await _fixture.Web3.Eth.GetBalance.SendRequestAsync(withdrawTo);

            Assert.Equal(withdrawAmount, balanceAfter.Value - balanceBefore.Value);
        }

        [Fact]
        public async Task SetVerifyingSigner_ByNonOwner_Fails()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var nonOwnerKey = new EthECKey(TestAccounts.Account3PrivateKey);
            var nonOwnerAddress = nonOwnerKey.GetPublicAddress();

            await _fixture.FundAccountAsync(nonOwnerAddress, 0.1m);

            var account = new Nethereum.Web3.Accounts.Account(TestAccounts.Account3PrivateKey);
            var nonOwnerWeb3 = new Web3.Web3(account, _fixture.Web3.Client);
            var nonOwnerPaymasterService = new VerifyingPaymasterService(nonOwnerWeb3, paymaster.ContractAddress);

            var newSigner = "0x2222222222222222222222222222222222222222";

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonOwnerPaymasterService.SetVerifyingSignerRequestAndWaitForReceiptAsync(newSigner));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task TransferOwnership_ByNonOwner_Fails()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            var nonOwnerKey = new EthECKey(TestAccounts.Account4PrivateKey);
            var nonOwnerAddress = nonOwnerKey.GetPublicAddress();

            await _fixture.FundAccountAsync(nonOwnerAddress, 0.1m);

            var account = new Nethereum.Web3.Accounts.Account(TestAccounts.Account4PrivateKey);
            var nonOwnerWeb3 = new Web3.Web3(account, _fixture.Web3.Client);
            var nonOwnerPaymasterService = new VerifyingPaymasterService(nonOwnerWeb3, paymaster.ContractAddress);

            var newOwner = "0x3333333333333333333333333333333333333333";

            var exception = await Assert.ThrowsAsync<SmartContractCustomErrorRevertException>(
                () => nonOwnerPaymasterService.TransferOwnershipRequestAndWaitForReceiptAsync(newOwner));

            Assert.True(exception.IsCustomErrorFor<OwnableUnauthorizedAccountError>());
        }

        [Fact]
        public async Task RenounceOwnership_ByOwner_RenounceOwnership()
        {
            var deployment = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment);

            await paymaster.RenounceOwnershipRequestAndWaitForReceiptAsync();

            var owner = await paymaster.OwnerQueryAsync();

            Assert.Equal("0x0000000000000000000000000000000000000000", owner.ToLower());
        }

        [Fact]
        public async Task MultiplePastmasters_IndependentDeposits()
        {
            var deployment1 = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var deployment2 = new VerifyingPaymasterDeployment
            {
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Owner = _fixture.BeneficiaryAddress,
                Signer = _fixture.BeneficiaryAddress
            };

            var paymaster1 = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment1);
            var paymaster2 = await VerifyingPaymasterService.DeployContractAndGetServiceAsync(
                _fixture.Web3, deployment2);

            var deposit1 = Web3.Web3.Convert.ToWei(0.1m);
            var deposit2 = Web3.Web3.Convert.ToWei(0.2m);

            await paymaster1.DepositRequestAndWaitForReceiptAsync(new DepositFunction { AmountToSend = deposit1 });
            await paymaster2.DepositRequestAndWaitForReceiptAsync(new DepositFunction { AmountToSend = deposit2 });

            var balance1 = await paymaster1.GetDepositQueryAsync();
            var balance2 = await paymaster2.GetDepositQueryAsync();

            Assert.Equal(deposit1, balance1);
            Assert.Equal(deposit2, balance2);
        }
    }
}
