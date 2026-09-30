using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.DevChain;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.CoreChain.UnitTests.DevChain
{
    /// <summary>
    /// AMS-7997-01/02/03. EIP-7997 §Specification: <i>"The chain's state MUST include
    /// <c>FACTORY_ADDRESS</c> with nonzero nonce and this runtime code"</i>,
    /// <i>"Client software MUST NOT check for the existence of the contract at the fork
    /// boundary"</i>, and the factory's own behaviour — salt from the first 32 bytes,
    /// init code from the rest, <i>"If input data is smaller than 32 bytes, the contract
    /// will attempt to copy close to 2^256 bytes of calldata and should revert as a
    /// result"</i>.
    ///
    /// <para>None of it is visible to the <c>eip7997</c> fixtures: all 16 cases ship the
    /// factory in their own <c>pre:</c> allocation, so they cannot see what our genesis
    /// builders write; a check that was never added cannot be observed failing; and the
    /// only two fixture calls that reach the factory both carry 48 bytes of input.</para>
    /// </summary>
    public class Eip7997DeterministicFactoryTests
    {
        private const string FactoryAddress = "0x4e59b44847b379578588920cA78FbF26c0B4956C";

        private const string SpecifiedFactoryRuntimeCode =
            "0x7fffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffffe03601600081602082378035828234f58015156039578182fd5b8082525050506014600cf3";

        private const string FundedAddress = "0xf39Fd6e51aad88F6F4ce6aB8827279cffFb92266";
        private const string FundedPrivateKey =
            "ac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80";
        private static readonly BigInteger ChainId = 31337;

        private const string InitCodeHex = "600a600c600039600a6000f3600160005260206000f3";
        private const string DeployedRuntimeHex = "600160005260206000f3";

        private static readonly BigInteger ShortInputCallGas = 1_000_000;

        [Fact]
        public async Task Given_DevChainGenesis_When_Built_Then_FactoryPresentWithExactRuntimeCodeAndNonce()
        {
            using var node = await StartAmsterdamNodeAsync();

            var code = await node.GetCodeAsync(FactoryAddress, 0);

            Assert.Equal(SpecifiedFactoryRuntimeCode, code.ToHex(true));
            Assert.True(await node.GetNonceAsync(FactoryAddress) > 0);
        }

        [Fact]
        public async Task Given_ADevChainPinnedBeforeAmsterdam_Then_TheFactoryIsNotAllocated()
        {
            using var node = DevChainNode.CreateInMemory(new DevChainConfig { Hardfork = "prague" });
            await node.StartAsync();

            Assert.Empty(await node.GetCodeAsync(FactoryAddress, 0) ?? Array.Empty<byte>());
        }

        /// <summary>
        /// EIP-7997 §Specification: <i>"Client software MUST NOT check for the existence
        /// of the contract at the fork boundary."</i> Shown as behaviour rather than as
        /// the absence of a grep hit.
        ///
        /// <para>What it can observe is block execution, not the boundary itself: the
        /// genesis builder allocates every predeploy the configured fork requires with no
        /// opt-out, so an Amsterdam genesis without the factory is not a state this
        /// codebase can build. Zeroing the account after genesis is the closest reachable
        /// state, and it leaves any check placed IN genesis or fork-config validation
        /// structurally unobservable here.</para>
        /// </summary>
        [Fact]
        public async Task Given_AnAmsterdamChainWithTheFactoryZeroedFromState_When_ABlockIsExecuted_Then_ExecutionProceeds()
        {
            using var node = await StartAmsterdamNodeAsync();
            await RemoveFactoryFromStateAsync(node);

            Assert.Empty(await node.GetCodeAsync(FactoryAddress) ?? Array.Empty<byte>());
            Assert.Equal(0, await node.GetNonceAsync(FactoryAddress));

            var result = await SendValueTransferAsync(node);
            Assert.True(result.Success, result.RevertReason);

            var header = await node.GetBlockByNumberAsync(await node.GetBlockNumberAsync());
            Assert.NotNull(header.BlockAccessListHash);
            Assert.NotNull(header.SlotNumber);
        }

        /// <summary>
        /// EIP-7997 §Specification: <i>"If input data is smaller than 32 bytes, the
        /// contract will attempt to copy close to 2^256 bytes of calldata and should
        /// revert as a result."</i>
        ///
        /// <para>The EIP says "revert"; what the EVM actually does is run out of gas on
        /// the memory expansion, and the two differ in the refund — so the assertion, and
        /// the name, are for the whole gas limit being consumed.</para>
        /// </summary>
        [Fact]
        public async Task Given_FactoryCalledWithShortInput_When_Executed_Then_TheCallFailsConsumingTheWholeGasLimit()
        {
            using var node = await StartAmsterdamNodeAsync();

            var result = await node.CallAsync(
                FactoryAddress, new byte[31], FundedAddress, gasLimit: ShortInputCallGas);

            Assert.False(result.Success);
            Assert.Equal(ShortInputCallGas, result.GasUsed);
        }

        [Fact]
        public async Task Given_FactoryCalledWithExactly32Bytes_When_Executed_Then_ItSucceeds()
        {
            using var node = await StartAmsterdamNodeAsync();

            var result = await node.CallAsync(FactoryAddress, new byte[32], FundedAddress);

            Assert.True(result.Success, result.RevertReason);
        }

        /// <summary>
        /// EIP-7997 §Specification: <i>"a salt equal to the first 32 bytes of the call's
        /// input data, init code equal to the remaining data"</i> and <i>"the address is
        /// returned in exactly 20 bytes of data with no padding"</i>.
        /// </summary>
        [Fact]
        public async Task Given_FactoryCalledWithSaltAndInitCode_When_Executed_Then_ItReturnsTheCreate2AddressInExactly20Bytes()
        {
            using var node = await StartAmsterdamNodeAsync();
            var salt = SaltOf(7);

            var result = await node.CallAsync(FactoryAddress, FactoryInput(salt), FundedAddress);

            Assert.True(result.Success, result.RevertReason);
            Assert.Equal(20, result.ReturnData.Length);
            Assert.Equal(ExpectedCreate2Address(salt).ToHex(), result.ReturnData.ToHex());
        }

        [Fact]
        public async Task Given_FactoryCalledWithTwoDifferentSalts_When_Executed_Then_TheAddressesDiffer()
        {
            using var node = await StartAmsterdamNodeAsync();

            var first = await node.CallAsync(FactoryAddress, FactoryInput(SaltOf(1)), FundedAddress);
            var second = await node.CallAsync(FactoryAddress, FactoryInput(SaltOf(2)), FundedAddress);

            Assert.True(first.Success, first.RevertReason);
            Assert.True(second.Success, second.RevertReason);
            Assert.NotEqual(first.ReturnData.ToHex(), second.ReturnData.ToHex());
        }

        [Fact]
        public async Task Given_FactoryCalledWithSaltAndInitCode_When_Mined_Then_TheDeployedRuntimeIsAtTheCreate2Address()
        {
            using var node = await StartAmsterdamNodeAsync();
            var salt = SaltOf(9);
            var expected = ExpectedCreate2Address(salt);

            var result = await SendToFactoryAsync(node, FactoryInput(salt));
            Assert.True(result.Success, result.RevertReason);

            var deployed = await node.GetCodeAsync(expected.ToHex(true));
            Assert.Equal(DeployedRuntimeHex, deployed.ToHex());
        }

        private static async Task<DevChainNode> StartAmsterdamNodeAsync()
        {
            var node = DevChainNode.CreateInMemory(
                new DevChainConfig { Hardfork = "amsterdam", ChainId = ChainId });
            await node.StartAsync(new[] { FundedAddress }, BigInteger.Parse("10000000000000000000000"));
            Assert.Equal(HardforkName.Amsterdam, HardforkNames.Parse(node.Config.Hardfork));
            return node;
        }

        private static async Task RemoveFactoryFromStateAsync(DevChainNode node)
        {
            await node.State.SaveAccountAsync(FactoryAddress, new Account
            {
                Balance = 0,
                Nonce = 0,
                CodeHash = DefaultValues.EMPTY_DATA_HASH
            });
        }

        private static Task<TransactionExecutionResult> SendValueTransferAsync(DevChainNode node)
            => SendAsync(node, "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC", 21_000, "");

        private static Task<TransactionExecutionResult> SendToFactoryAsync(DevChainNode node, byte[] input)
            => SendAsync(node, FactoryAddress, 1_000_000, input.ToHex());

        private static async Task<TransactionExecutionResult> SendAsync(
            DevChainNode node, string to, BigInteger gasLimit, string data)
        {
            var nonce = await node.GetNonceAsync(FundedAddress);
            var signedTxHex = new LegacyTransactionSigner().SignTransaction(
                FundedPrivateKey.HexToByteArray(), ChainId, to, BigInteger.Zero, nonce,
                1_000_000_000, gasLimit, data);
            return await node.SendTransactionAsync(TransactionFactory.CreateTransaction(signedTxHex));
        }

        private static byte[] SaltOf(byte value)
        {
            var salt = new byte[32];
            salt[31] = value;
            return salt;
        }

        private static byte[] FactoryInput(byte[] salt)
            => ByteUtil.Merge(salt, InitCodeHex.HexToByteArray());

        private static byte[] ExpectedCreate2Address(byte[] salt)
        {
            var initCodeHash = Sha3Keccack.Current.CalculateHash(InitCodeHex.HexToByteArray());
            var preimage = ByteUtil.Merge(
                new byte[] { 0xff }, FactoryAddress.HexToByteArray(), salt, initCodeHash);
            var hash = Sha3Keccack.Current.CalculateHash(preimage);

            var address = new byte[20];
            Array.Copy(hash, 12, address, 0, 20);
            return address;
        }
    }
}
