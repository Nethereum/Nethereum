using System;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.Bundler;
using Nethereum.AccountAbstraction.Bundler.Execution;
using Nethereum.AccountAbstraction.Bundler.Mempool;
using Nethereum.AccountAbstraction.EntryPoint.ContractDefinition;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.AccountAbstraction.Structs;
using Nethereum.Contracts;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EIP7702-4337")]
    public class Eip7702HandleOpsBundleTests
    {
        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eip7702HandleOpsBundleTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task HandleOpsBundle_WithEip7702Auth_DelegatesAuthorityOnChain_ViaType4Transaction()
        {
            var (accountAddress, packedOp) = await CreateWorkingBundleOpAsync(salt: 7701);

            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            var delegateTarget = _fixture.AccountFactoryService.ContractAddress;

            var codeBefore = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "authority must start as a plain EOA");

            var auth = _fixture.SignAuthorization(authorityKey, delegateTarget, nonce: 0).ToRPCAuthorisation();
            var bundle = BuildBundle(packedOp,
                await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedOp),
                auth);

            var result = await CreateExecutor().ExecuteAsync(bundle);
            _output.WriteLine($"handleOps success={result.Success}, tx={result.TransactionHash}, error={result.Error}");

            Assert.True(result.Success, $"handleOps bundle should have mined: {result.Error}");

            var accountCode = await _fixture.GetCodeAsync(accountAddress);
            Assert.True((accountCode?.Length ?? 0) > 0, "the bundled UserOperation should have executed");

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            Assert.Equal(0x01, codeAfter[1]);
            Assert.Equal(0x00, codeAfter[2]);
            var delegated = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(delegateTarget.ToLowerInvariant(), delegated.ToLowerInvariant());
            _output.WriteLine($"authority {authorityAddress} delegated to {delegated} via the handleOps bundle tx");
        }

        [Fact]
        public async Task HandleOpsBundle_WithoutEip7702Auth_MinesNormalTransaction_NoDelegation()
        {
            var (accountAddress, packedOp) = await CreateWorkingBundleOpAsync(salt: 7702);

            var bundle = BuildBundle(packedOp,
                await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedOp),
                eip7702Auth: null);

            var result = await CreateExecutor().ExecuteAsync(bundle);
            _output.WriteLine($"handleOps success={result.Success}, tx={result.TransactionHash}, error={result.Error}");

            Assert.True(result.Success, $"non-7702 handleOps bundle should have mined: {result.Error}");
            var accountCode = await _fixture.GetCodeAsync(accountAddress);
            Assert.True((accountCode?.Length ?? 0) > 0, "the bundled UserOperation should have executed");
        }

        [Fact]
        public async Task HandleOpsBundle_SkipsTupleForAlreadyDelegatedAuthority()
        {
            var delegateTarget = _fixture.AccountFactoryService.ContractAddress;

            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            var firstAuth = _fixture.SignAuthorization(authorityKey, delegateTarget, nonce: 0);
            var predelegateTx = _fixture.CreateType4Transaction(
                await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address),
                _fixture.BundlerAccount.Address,
                new System.Collections.Generic.List<Nethereum.Model.Authorisation7702Signed> { firstAuth });
            var predelegateResult = await _fixture.Node.SendTransactionAsync(predelegateTx);
            Assert.True(predelegateResult.Success, $"pre-delegation failed: {predelegateResult.RevertReason}");
            var nonceAfterFirstDelegation = await _fixture.GetNonceAsync(authorityAddress);
            Assert.Equal(System.Numerics.BigInteger.One, nonceAfterFirstDelegation);

            var (_, packedOp) = await CreateWorkingBundleOpAsync(salt: 7703);
            var redundantAuth = _fixture
                .SignAuthorization(authorityKey, delegateTarget, nonceAfterFirstDelegation)
                .ToRPCAuthorisation();
            var bundle = BuildBundle(packedOp,
                await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedOp),
                redundantAuth);

            var result = await CreateExecutor().ExecuteAsync(bundle);
            Assert.True(result.Success, $"handleOps bundle should have mined: {result.Error}");

            var nonceNow = await _fixture.GetNonceAsync(authorityAddress);
            Assert.Equal(nonceAfterFirstDelegation, nonceNow);
        }

        private async Task<(string accountAddress, Nethereum.AccountAbstraction.Structs.PackedUserOperation packedOp)>
            CreateWorkingBundleOpAsync(ulong salt)
        {
            var accountKey = Nethereum.Signer.EthECKey.GenerateKey();
            var ownerAddress = accountKey.GetPublicAddress();

            var accountAddress = await _fixture.AccountFactoryService.GetAddressQueryAsync(ownerAddress, salt);
            await _fixture.FundAccountAsync(accountAddress, 2m);

            var initCode = _fixture.AccountFactoryService.GetCreateAccountInitCode(ownerAddress, salt);

            var userOp = new Nethereum.AccountAbstraction.UserOperation
            {
                Sender = accountAddress,
                Nonce = 0,
                InitCode = initCode,
                CallData = Array.Empty<byte>(),
                CallGasLimit = 50000,
                VerificationGasLimit = 500000,
                PreVerificationGas = 50000,
                MaxFeePerGas = 2000000000,
                MaxPriorityFeePerGas = 1000000000
            };

            var packedOp = await _fixture.EntryPointService.SignAndInitialiseUserOperationAsync(userOp, accountKey);
            return (accountAddress, packedOp);
        }

        [Fact]
        public async Task HandleOpsBundle_SenderIsAuthority_EstimatesWithoutReverting_MinesAndExecutes()
        {
            var senderKey = EthECKey.GenerateKey();
            var senderAddress = senderKey.GetPublicAddress();

            var delegateAccount = await DeployRuntimeAsync("600060005260206000f3");

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = senderAddress,
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m)
                });

            var codeBefore = await _fixture.GetCodeAsync(senderAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0, "sender must start as a code-less EOA");

            var packedOp = BuildMinimalPackedOp(senderAddress);
            var auth = _fixture.SignAuthorization(senderKey, delegateAccount, nonce: 0).ToRPCAuthorisation();
            var bundle = BuildBundle(packedOp,
                await _fixture.EntryPointService.GetUserOpHashQueryAsync(packedOp),
                auth);

            var result = await CreateExecutor().ExecuteAsync(bundle);
            _output.WriteLine($"success={result.Success}, tx={result.TransactionHash}, error={result.Error}");

            Assert.True(result.Success, $"sender==authority handleOps should have mined: {result.Error}");

            var codeAfter = await _fixture.GetCodeAsync(senderAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Equal(0xef, codeAfter[0]);
            var delegated = "0x" + codeAfter.Skip(3).ToArray().ToHex();
            Assert.Equal(delegateAccount.ToLowerInvariant(), delegated.ToLowerInvariant());

            Assert.Single(result.UserOpResults);
            Assert.True(result.UserOpResults[0].EventFound, "the UserOperation should have executed in the bundle");
            Assert.True(result.UserOpResults[0].Success, "the UserOperation should have succeeded");
        }

        [Fact]
        public async Task HandleOpsBundle_Factory7702_WithFactoryData_RunsPostDelegationInit_MinesAndExecutes()
        {
            var senderKey = EthECKey.GenerateKey();
            var senderAddress = senderKey.GetPublicAddress();

            var delegateAccount = await DeployRuntimeAsync("600060005260206000f3");

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = senderAddress,
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m)
                });

            var factoryData = "0x0c55699c00000000000000000000000000000000000000000000000000000000000012fd".HexToByteArray();
            var packedOp = BuildMinimalPackedOp(senderAddress);
            packedOp.InitCode = Nethereum.AccountAbstraction.AAEIP7702Utils.BuildInitCode("0x7702", factoryData);

            Assert.True(Nethereum.AccountAbstraction.AAEIP7702Utils.IsEip7702UserOp(packedOp.InitCode));
            Assert.Equal(
                Nethereum.AccountAbstraction.AAEIP7702Utils.INITCODE_EIP7702_MARKER_ADDRESS,
                packedOp.InitCode.Take(20).ToArray());
            Assert.Equal(factoryData, packedOp.InitCode.Skip(20).ToArray());

            var auth = _fixture.SignAuthorization(senderKey, delegateAccount, nonce: 0).ToRPCAuthorisation();
            var bundle = BuildBundle(packedOp,
                await GetUserOpHashWith7702OverrideAsync(packedOp, delegateAccount),
                auth);

            var result = await CreateExecutor().ExecuteAsync(bundle);
            _output.WriteLine($"success={result.Success}, tx={result.TransactionHash}, error={result.Error}");

            Assert.True(result.Success, $"factory=0x7702 with factoryData should have mined: {result.Error}");
            var codeAfter = await _fixture.GetCodeAsync(senderAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
            Assert.Single(result.UserOpResults);
            Assert.True(result.UserOpResults[0].EventFound, "the UserOperation should have executed in the bundle");
            Assert.True(result.UserOpResults[0].Success, "the UserOperation should have succeeded");
        }

        [Fact]
        public async Task HandleOpsBundle_Factory7702_NoFactoryData_PaddedMarker_MinesAndDelegates()
        {
            var senderKey = EthECKey.GenerateKey();
            var senderAddress = senderKey.GetPublicAddress();

            var delegateAccount = await DeployRuntimeAsync("600060005260206000f3");

            await _fixture.EntryPointService.DepositToRequestAndWaitForReceiptAsync(
                new DepositToFunction
                {
                    Account = senderAddress,
                    AmountToSend = Nethereum.Web3.Web3.Convert.ToWei(1m)
                });

            var packedOp = BuildMinimalPackedOp(senderAddress);
            packedOp.InitCode = Nethereum.AccountAbstraction.AAEIP7702Utils.BuildInitCode(
                "0x7702", System.Array.Empty<byte>());
            Assert.Equal(20, packedOp.InitCode.Length);

            var auth = _fixture.SignAuthorization(senderKey, delegateAccount, nonce: 0).ToRPCAuthorisation();
            var bundle = BuildBundle(packedOp,
                await GetUserOpHashWith7702OverrideAsync(packedOp, delegateAccount),
                auth);

            var result = await CreateExecutor().ExecuteAsync(bundle);
            _output.WriteLine($"success={result.Success}, tx={result.TransactionHash}, error={result.Error}");

            Assert.True(result.Success, $"factory=0x7702 no-factoryData should have mined: {result.Error}");
            var codeAfter = await _fixture.GetCodeAsync(senderAddress);
            Assert.NotNull(codeAfter);
            Assert.Equal(23, codeAfter.Length);
        }

        private async Task<string> DeployRuntimeAsync(string runtimeHex)
        {
            var runtimeLen = runtimeHex.Length / 2;
            var pushOpcode = (0x60 + runtimeLen - 1).ToString("x2");
            var lenHex = runtimeLen.ToString("x2");
            var offsetHex = (32 - runtimeLen).ToString("x2");
            var initCode = $"{pushOpcode}{runtimeHex}600052{"60" + lenHex}{"60" + offsetHex}f3";

            var nonce = await _fixture.Node.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signedTxHex = new LegacyTransactionSigner().SignTransaction(
                _fixture.OperatorPrivateKey.Substring(2).HexToByteArray(),
                DevChainBundlerFixture.CHAIN_ID,
                "",
                BigInteger.Zero,
                nonce,
                1_000_000_000,
                3_000_000,
                initCode);
            var tx = TransactionFactory.CreateTransaction(signedTxHex);
            var result = await _fixture.Node.SendTransactionAsync(tx);
            Assert.True(result.Success, $"delegate account deployment failed: {result.RevertReason}");
            var receipt = await _fixture.Node.GetTransactionReceiptInfoAsync(tx.Hash);
            return receipt.ContractAddress;
        }

        private async Task<byte[]> GetUserOpHashWith7702OverrideAsync(PackedUserOperation packedOp, string delegateAddress)
        {
            var function = new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.GetUserOpHashFunction
            {
                UserOp = packedOp
            };
            var callInput = function.CreateTransactionInput(_fixture.EntryPointService.ContractAddress);

            var stateOverride = new System.Collections.Generic.Dictionary<string, Nethereum.RPC.Eth.DTOs.StateChange>
            {
                [packedOp.Sender] = new Nethereum.RPC.Eth.DTOs.StateChange
                {
                    Code = Nethereum.EVM.Execution.Eip7702DelegationUtils
                        .CreateDelegationCode(delegateAddress).ToHex(true)
                }
            };

            var raw = await new Nethereum.Geth.RPC.GethEth.EthCall(_fixture.Web3.Client)
                .SendRequestAsync(callInput, Nethereum.RPC.Eth.DTOs.BlockParameter.CreateLatest(), stateOverride);
            return new Nethereum.ABI.FunctionEncoding.FunctionCallDecoder()
                .DecodeFunctionOutput(
                    new Nethereum.AccountAbstraction.EntryPoint.ContractDefinition.GetUserOpHashOutputDTO(), raw)
                .ReturnValue1;
        }

        private static PackedUserOperation BuildMinimalPackedOp(string sender)
        {
            var accountGasLimits = new byte[32];
            PackBigEndian(accountGasLimits, 8, 200_000);
            PackBigEndian(accountGasLimits, 24, 100_000);
            var gasFees = new byte[32];
            PackBigEndian(gasFees, 8, 1_000_000_000);
            PackBigEndian(gasFees, 24, 2_000_000_000);

            return new PackedUserOperation
            {
                Sender = sender,
                Nonce = BigInteger.Zero,
                InitCode = Array.Empty<byte>(),
                CallData = Array.Empty<byte>(),
                AccountGasLimits = accountGasLimits,
                PreVerificationGas = 50_000,
                GasFees = gasFees,
                PaymasterAndData = Array.Empty<byte>(),
                Signature = new byte[65]
            };
        }

        private static void PackBigEndian(byte[] target, int offset, long value)
        {
            var bytes = BitConverter.GetBytes(value);
            Array.Reverse(bytes);
            Array.Copy(bytes, 0, target, offset, 8);
        }

        private BundleExecutor CreateExecutor()
        {
            var bundlerWeb3 = _fixture.Node.CreateWeb3(_fixture.BundlerAccount);
            var config = new BundlerConfig
            {
                SupportedEntryPoints = new[] { _fixture.EntryPointService.ContractAddress },
                BeneficiaryAddress = _fixture.BundlerAccount.Address,
                ChainId = DevChainBundlerFixture.CHAIN_ID
            };
            return new BundleExecutor(bundlerWeb3, config);
        }

        private Bundle BuildBundle(
            Nethereum.AccountAbstraction.Structs.PackedUserOperation packedOp,
            byte[] userOpHash,
            Nethereum.RPC.Eth.DTOs.Authorisation? eip7702Auth)
        {
            var entry = new MempoolEntry
            {
                UserOpHash = userOpHash.ToHex(true),
                UserOperation = packedOp,
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Eip7702Auth = eip7702Auth
            };

            return new Bundle
            {
                Entries = new[] { entry },
                EntryPoint = _fixture.EntryPointService.ContractAddress,
                Beneficiary = _fixture.BundlerAccount.Address
            };
        }
    }
}
