using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AccountAbstraction.IntegrationTests.E2E.Fixtures;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RPC.Eth.DTOs;
using Nethereum.RPC.Eth.Mappers;
using Nethereum.Signer;
using Xunit;
using Xunit.Abstractions;
using GethEthCall = Nethereum.Geth.RPC.GethEth.EthCall;

namespace Nethereum.AccountAbstraction.IntegrationTests.E2E
{
    [Collection(DevChainBundlerFixture.COLLECTION_NAME)]
    [Trait("Category", "EIP7702-4337")]
    public class Eip7702EthCallAuthorizationListTests
    {
        private const string ImplRuntimeHex = "604260005260206000f3";
        private const string ImplInitHex = "600a600c600039600a6000f3" + ImplRuntimeHex;

        private static readonly byte[] Value42 =
            "0x0000000000000000000000000000000000000000000000000000000000000042".HexToByteArray();

        private readonly DevChainBundlerFixture _fixture;
        private readonly ITestOutputHelper _output;

        public Eip7702EthCallAuthorizationListTests(DevChainBundlerFixture fixture, ITestOutputHelper output)
        {
            _fixture = fixture;
            _output = output;
        }

        [Fact]
        public async Task NodeCall_ValidAuthorization_DelegatesEphemerally()
        {
            var implAddress = await DeployImplAsync();
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 1m);

            var validAuth = _fixture.SignAuthorization(authorityKey, implAddress, nonce: 0);

            var result = await _fixture.Node.CallAsync(
                authorityAddress, System.Array.Empty<byte>(),
                authorisationList: new List<Authorisation7702Signed> { validAuth });

            _output.WriteLine($"Valid-auth call success={result.Success} return={result.ReturnData?.ToHex()}");
            Assert.True(result.Success, result.RevertReason);
            Assert.Equal(Value42.ToHex(), result.ReturnData.ToHex());

            var codeAfter = await _fixture.GetCodeAsync(authorityAddress);
            Assert.True(codeAfter == null || codeAfter.Length == 0, "auth must not persist to canonical state");
        }

        [Fact]
        public async Task NodeCall_WrongNonceAuthorization_DoesNotDelegate()
        {
            var implAddress = await DeployImplAsync();
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 1m);

            var wrongAuth = _fixture.SignAuthorization(authorityKey, implAddress, nonce: 99);

            var result = await _fixture.Node.CallAsync(
                authorityAddress, System.Array.Empty<byte>(),
                authorisationList: new List<Authorisation7702Signed> { wrongAuth });

            _output.WriteLine($"Wrong-nonce call success={result.Success} return={result.ReturnData?.ToHex()}");
            Assert.True(result.Success, result.RevertReason);
            Assert.True(result.ReturnData == null || result.ReturnData.Length == 0);
        }

        [Fact]
        public async Task EthCallRpc_ValidVsWrongNonce_MatchesDelegation()
        {
            var implAddress = await DeployImplAsync();
            var (authorityKey, authorityAddress) = _fixture.GenerateNewAccount();
            await _fixture.FundAccountAsync(authorityAddress, 1m);

            var validReturn = await EthCallWithAuthAsync(
                authorityAddress, _fixture.SignAuthorization(authorityKey, implAddress, nonce: 0));
            _output.WriteLine($"RPC valid-auth return={validReturn}");
            Assert.Equal(Value42.ToHex(), validReturn.HexToByteArray().ToHex());

            var wrongReturn = await EthCallWithAuthAsync(
                authorityAddress, _fixture.SignAuthorization(authorityKey, implAddress, nonce: 99));
            _output.WriteLine($"RPC wrong-nonce return={wrongReturn}");
            Assert.True(wrongReturn == "0x" || string.IsNullOrEmpty(wrongReturn.HexToByteArray().ToHex()));
        }

        private async Task<string> EthCallWithAuthAsync(string to, Authorisation7702Signed auth)
        {
            var callInput = new TransactionInput
            {
                To = to,
                Data = "0x",
                Gas = new Nethereum.Hex.HexTypes.HexBigInteger(1_000_000),
                AuthorisationList = new List<Authorisation> { auth.ToRPCAuthorisation() }
            };

            var ethCall = new GethEthCall(_fixture.Web3.Client);
            return await ethCall.SendRequestAsync(
                callInput, BlockParameter.CreateLatest(), new Dictionary<string, StateChange>());
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
            var deployed = await _fixture.GetCodeAsync(receipt.ContractAddress);
            Assert.Equal(ImplRuntimeHex, deployed.ToHex());
            return receipt.ContractAddress;
        }
    }
}
