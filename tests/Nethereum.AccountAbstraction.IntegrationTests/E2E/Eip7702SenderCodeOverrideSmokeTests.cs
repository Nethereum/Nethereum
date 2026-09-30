using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.EVM.Execution;
using Nethereum.Geth.RPC.GethEth;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.Signer;
using Newtonsoft.Json.Linq;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EIP7702-4337")]
    public class Eip7702SenderCodeOverrideSmokeTests
    {
        private const string ImplRuntimeHex = "60005460005260206000f3";

        private const string ImplInitHex = "600b600c600039600b6000f3" + ImplRuntimeHex;

        private static readonly string Slot0 =
            "0x0000000000000000000000000000000000000000000000000000000000000000";
        private static readonly string PreseededValue =
            "0x0000000000000000000000000000000000000000000000000000000000000042";

        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eip7702SenderCodeOverrideSmokeTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task OverrideInjectedDelegation_RunsDelegateLogic_AgainstSenderStorage()
        {
            var implAddress = await DeployImplAsync();
            var deployedRuntime = await _fixture.GetCodeAsync(implAddress);
            Assert.Equal(ImplRuntimeHex, deployedRuntime.ToHex());
            _output.WriteLine($"Delegate impl deployed at {implAddress}: {deployedRuntime.ToHex()}");

            var (_, eoaAddress) = _fixture.GenerateNewAccount();
            var codeBefore = await _fixture.GetCodeAsync(eoaAddress);
            Assert.True(codeBefore == null || codeBefore.Length == 0);

            var delegationCode = Eip7702DelegationUtils.CreateDelegationCode(implAddress).ToHex(true);
            _output.WriteLine($"Injecting EOA code = {delegationCode}");

            var returned = await CallWithSenderOverrideAsync(
                eoaAddress, delegationCode, preseedSlot0: PreseededValue);

            _output.WriteLine($"Returned: {returned}");
            Assert.Equal(
                PreseededValue.HexToByteArray().ToHex(),
                returned.HexToByteArray().ToHex());
        }

        [Fact]
        public async Task OverrideInjectedDelegation_ReadsSenderStorage_NotDelegateStorage()
        {
            var implAddress = await DeployImplAsync();
            var (_, eoaAddress) = _fixture.GenerateNewAccount();

            var delegationCode = Eip7702DelegationUtils.CreateDelegationCode(implAddress).ToHex(true);

            var returned = await CallWithSenderOverrideAsync(
                eoaAddress, delegationCode, preseedSlot0: null);

            var value = new BigInteger(returned.HexToByteArray().Reverse().ToArray());
            _output.WriteLine($"Returned (no preseed): {returned}");
            Assert.Equal(BigInteger.Zero, value);
        }

        private async Task<string> CallWithSenderOverrideAsync(
            string eoaAddress, string delegationCode, string preseedSlot0)
        {
            var senderOverride = new StateChange { Code = delegationCode };
            if (preseedSlot0 != null)
            {
                senderOverride.State = new JObject { [Slot0] = preseedSlot0 };
            }

            var stateOverride = new Dictionary<string, StateChange>
            {
                [eoaAddress] = senderOverride
            };

            var callInput = new TransactionInput
            {
                To = eoaAddress,
                Data = "0x",
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(1_000_000)
            };

            var ethCall = new EthCall(_fixture.Web3.Client);
            return await ethCall.SendRequestAsync(callInput, BlockParameter.CreateLatest(), stateOverride);
        }

        private async Task<string> DeployImplAsync()
        {
            var nonce = await _fixture.GetNonceAsync(_fixture.OperatorAccount.Address);
            var signer = new LegacyTransactionSigner();

            var signedTxHex = signer.SignTransaction(
                _fixture.OperatorPrivateKey.Substring(2).HexToByteArray(),
                DevChainBundlerFixture.CHAIN_ID,
                "",
                BigInteger.Zero,
                nonce,
                1_000_000_000,
                3_000_000,
                ImplInitHex);

            var tx = TransactionFactory.CreateTransaction(signedTxHex);
            var result = await _fixture.Node.SendTransactionAsync(tx);
            Assert.True(result.Success, $"Impl deployment failed: {result.RevertReason}");

            var receipt = await _fixture.Node.GetTransactionReceiptInfoAsync(tx.Hash);
            return receipt.ContractAddress;
        }
    }
}
