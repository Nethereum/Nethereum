using System.Numerics;
using System.Linq;
using System.Threading.Tasks;
using Nethereum.CoreChain;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Binary.Hashing;
using Nethereum.Merkle.Binary.Keys;
using Nethereum.Merkle.Binary.Proofs;
using Nethereum.Util;
using Xunit;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests
{
    public class BinaryDevChainE2ETests
    {
        private readonly ITestOutputHelper _output;

        public BinaryDevChainE2ETests(ITestOutputHelper output)
        {
            _output = output;
        }

        // Cross-validated against jsign/binary-tree-spec on 2026-04-21.
        // One account: address=0x1000..., balance=1000 ETH, nonce=0, no code.
        [Fact]
        public async Task BinaryDevChain_GenesisRoot_MatchesJsignVector()
        {
            var config = new DevChainConfig
            {
                StateTree = StateTreeType.Binary,
                StateTreeHashProvider = new Blake3HashProvider(),
                InitialBalance = BigInteger.Parse("1000000000000000000000")
            };

            using var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync(new[] { "0x1000000000000000000000000000000000000000" });

            var genesis = await node.GetLatestBlockAsync();
            Assert.Equal(0, genesis.BlockNumber);

            Assert.Equal(
                "00fb5ddaef53cd750ca192835d0efb2475425a626a4fe934297d7318aadf66ce",
                genesis.StateRoot.ToHex());

            _output.WriteLine($"Genesis root (jsign-validated): {genesis.StateRoot.ToHex(true)}");
        }

        // Full E2E: binary DevChain → produce block → generate proof → verify
        // cryptographically → extract correct balance.
        [Fact]
        public async Task BinaryDevChain_ProduceBlock_ProofVerifies()
        {
            var config = new DevChainConfig
            {
                StateTree = StateTreeType.Binary,
                StateTreeHashProvider = new Blake3HashProvider()
            };

            using var node = DevChainNode.CreateInMemory(config);
            await node.StartAsync();

            var recipient = "0x1111111111111111111111111111111111111111";
            var oneEth = BigInteger.Parse("1000000000000000000");

            await node.SetBalanceAsync(recipient, oneEth);
            await node.MineBlockAsync();

            var block1 = await node.GetLatestBlockAsync();
            Assert.Equal(1, block1.BlockNumber);

            var balance = await node.GetBalanceAsync(recipient);
            Assert.Equal(oneEth, balance);

            // Proof via IProofService (dispatched to BinaryProofService)
            var proofService = node.ProofService;
            Assert.IsType<Services.BinaryProofService>(proofService);

            var accountProof = await proofService.GenerateAccountProofAsync(
                recipient, null, block1.StateRoot);

            Assert.Equal(oneEth, accountProof.Balance.Value);
            Assert.NotEmpty(accountProof.AccountProofs);

            // Verify proof cryptographically
            var hashProvider = new Blake3HashProvider();
            var verifier = new BinaryTrieProofVerifier(hashProvider);
            var keyDerivation = new BinaryTreeKeyDerivation(hashProvider);

            var addressBytes = AddressUtil.Current
                .ConvertToValid20ByteAddress(recipient).HexToByteArray();
            var basicKey = keyDerivation.GetTreeKeyForBasicData(addressBytes);

            var proofObj = new BinaryTrieProof
            {
                Nodes = accountProof.AccountProofs
                    .Select(hex => hex.HexToByteArray()).ToArray()
            };

            var verifiedLeaf = verifier.VerifyProof(block1.StateRoot, basicKey, proofObj);
            Assert.NotNull(verifiedLeaf);

            BasicDataLeaf.Unpack(verifiedLeaf,
                out var version, out var codeSize, out var nonce, out var verifiedBalance);

            Assert.Equal(oneEth, (BigInteger)verifiedBalance);

            _output.WriteLine($"Block 1 root: {block1.StateRoot.ToHex(true)}");
            _output.WriteLine($"Balance from verified proof: {(BigInteger)verifiedBalance}");
        }
    }
}
