using System;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.AppChain.Anchoring;
using Nethereum.AppChain.Anchoring.AppChainAnchor.ContractDefinition;
using Nethereum.CoreChain.IntegrationTests.Fixtures;
using Nethereum.DevChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Signer;
using Xunit.Abstractions;

namespace Nethereum.CoreChain.IntegrationTests.Strategies
{
    public abstract class StrategyContractTestBase
    {
        protected readonly DevChainAnchorFixture Fixture;
        protected readonly ITestOutputHelper Output;
        private readonly LegacyTransactionSigner _signer = new();
        private static int _chainCounter = 60000;

        protected StrategyContractTestBase(DevChainAnchorFixture fixture, ITestOutputHelper output)
        {
            Fixture = fixture;
            Output = output;
        }

        protected async Task<(ulong chainId, byte[] genesisHash)> RegisterChain(byte minimumProofSystem = 0)
        {
            var id = System.Threading.Interlocked.Increment(ref _chainCounter);
            var chainId = (ulong)id;
            var genesisHash = new Nethereum.Util.Sha3Keccack()
                .CalculateHash(System.Text.Encoding.UTF8.GetBytes($"strategy-{id}-{DateTime.UtcNow.Ticks}"));

            await Fixture.AnchorService.RegisterAppChainRequestAndWaitForReceiptAsync(
                new RegisterAppChainFunction
                {
                    ChainId = chainId, GenesisHash = genesisHash, GenesisBlock = 1,
                    GenesisStateRoot = new byte[32],
                    MinimumProofSystem = minimumProofSystem, MinimumAnchorVersion = 1,
                    Authority = Fixture.AuthorityService.ContractAddress
                });

            await Fixture.AuthorityService.SetOperatorRequestAndWaitForReceiptAsync(
                new Nethereum.AppChain.Anchoring.SimpleAuthority.ContractDefinition.SetOperatorFunction
                { ChainId = chainId, NewOperator = Fixture.OperatorAccount.Address });

            return (chainId, genesisHash);
        }

        protected async Task<DevChainNode> ProduceAppChain(int blocks)
        {
            var appchain = DevChainNode.CreateInMemory(new DevChainConfig
            {
                ChainId = 31337, BlockGasLimit = 30_000_000, AutoMine = false
            });
            await appchain.StartAsync(new[] { Fixture.OperatorAccount.Address });

            var pkBytes = Fixture.OperatorPrivateKey.Substring(2).HexToByteArray();
            ulong nonce = 0;
            for (int b = 0; b < blocks; b++)
            {
                var txHex = _signer.SignTransaction(pkBytes, (BigInteger)31337,
                    $"0x{(b + 1):x40}", 1000, nonce++, 1_000_000_000, 21_000, "");
                await appchain.SendTransactionAsync(TransactionFactory.CreateTransaction(txHex));
                await appchain.MineBlockAsync();
            }
            return appchain;
        }

        protected AppChainAnchorBatchService CreateBatchService(ulong chainId, byte[] genesisHash)
        {
            return new AppChainAnchorBatchService(
                new AnchorConfig { AnchorContractAddress = Fixture.AnchorService.ContractAddress },
                Fixture.OperatorWeb3, chainId, genesisHash);
        }

        protected async Task RegisterProofSystem(byte proofSystem, bool requiresProof = false,
            string verifier = "0x0000000000000000000000000000000000000000")
        {
            await Fixture.AnchorService.RegisterProofSystemRequestAndWaitForReceiptAsync(
                new RegisterProofSystemFunction
                {
                    ProofSystem = proofSystem,
                    Verifier = verifier,
                    RequiresProof = requiresProof
                });
        }
    }

    public class L1NodeChainAnchorable : IChainAnchorable
    {
        private readonly DevChainNode _node;
        public L1NodeChainAnchorable(DevChainNode node) { _node = node; }

        public async Task<BigInteger> GetBlockNumberAsync()
        {
            var latest = await _node.GetBlockByNumberAsync(-1);
            return latest?.BlockNumber ?? 0;
        }

        public Task<BlockHeader?> GetBlockByNumberAsync(BigInteger blockNumber)
            => _node.GetBlockByNumberAsync((long)blockNumber);

        public Task<byte[]?> GetBlockHashByNumberAsync(BigInteger blockNumber)
            => _node.GetBlockHashByNumberAsync((ulong)blockNumber);
    }
}
