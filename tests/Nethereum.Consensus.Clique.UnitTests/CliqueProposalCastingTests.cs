using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Consensus.Clique;
using Nethereum.CoreChain;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Storage.InMemory;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Xunit;

namespace Nethereum.Consensus.Clique.UnitTests
{
    public class CliqueProposalCastingTests
    {
        private static readonly EthECKey KeyA = new EthECKey("0xac0974bec39a17e36ba4a6b4d238ff944bacb478cbed5efcae784d7bf4f2ff80");
        private static readonly EthECKey KeyB = new EthECKey("0x59c6995e998f97a5a0044966f0945389dc9e86dae88c7a8412f4603b6b78690");
        private static readonly EthECKey KeyC = new EthECKey("0x5de4111afa1a4b94908f83103eb1f1706367c2e68ca870fc3fb9a804cdab365");
        private static readonly EthECKey Target = new EthECKey("0xdbda1821b80551c9d65939329250298aa3472ba22feea921c0cf5d620ea67b97");

        private static CliqueEngine EngineWithSigners(EthECKey local, params EthECKey[] signers) => new CliqueEngine(new CliqueConfig
        {
            InitialSigners = signers.Select(k => k.GetPublicAddress()).ToList(),
            LocalSignerAddress = local.GetPublicAddress(),
            LocalSignerPrivateKey = local.GetPrivateKey(),
            BlockPeriodSeconds = 1,
            EpochLength = 30000
        });

        private static BlockHeader BuildSealedHeader(EthECKey signer, long number, BigInteger difficulty, string coinbase, byte[] nonce)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
                Difficulty = (EvmUInt256)difficulty,
                MixHash = new byte[32],
                Nonce = nonce,
                Coinbase = coinbase,
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = number
            };

            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = signer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        private static BlockHeader BuildSealedHeaderWithFullFields(
            EthECKey signer, long number, BigInteger difficulty, string coinbase, byte[] nonce,
            long timestamp, long gasLimit, BigInteger baseFee)
        {
            var header = new BlockHeader
            {
                BlockNumber = number,
                ParentHash = new byte[32],
                Difficulty = (EvmUInt256)difficulty,
                MixHash = new byte[32],
                Nonce = nonce,
                Coinbase = coinbase,
                ExtraData = new byte[CliqueEngine.EXTRA_VANITY + CliqueEngine.EXTRA_SEAL],
                Timestamp = timestamp,
                GasLimit = gasLimit,
                BaseFee = baseFee
            };

            var sealHash = BlockHeaderEncoder.Current.EncodeCliqueSigHeaderAndHash(header);
            var signature = signer.SignAndCalculateV(sealHash).CreateStringSignature().HexToByteArray();
            Array.Copy(signature, 0, header.ExtraData, header.ExtraData.Length - CliqueEngine.EXTRA_SEAL, CliqueEngine.EXTRA_SEAL);
            return header;
        }

        private static void ApplyVoteBlock(CliqueEngine engine, EthECKey signer, long number, string coinbase, bool authorize)
        {
            var nonce = authorize ? CliqueEngine.NONCE_AUTH : CliqueEngine.NONCE_DROP;
            var header = BuildSealedHeader(signer, number, engine.GetDifficulty(number, signer.GetPublicAddress()), coinbase, nonce);
            engine.ApplyBlock(header, signer.GetPublicAddress());
        }

        [Fact]
        public void Given_APendingAuthorizeProposal_When_BlockOptionsArePrepared_Then_CoinbaseIsTargetAndNonceIsAuth()
        {
            var store = new InMemoryCliqueProposalStore();
            store.SetProposal(Target.GetPublicAddress(), authorize: true);
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            var strategy = new CliqueBlockProductionStrategy(new ChainConfig(), engine, proposalStore: store);

            var options = strategy.PrepareBlockOptions(1, null);

            Assert.True(options.Coinbase.IsTheSameAddress(Target.GetPublicAddress()));
            Assert.Equal(CliqueEngine.NONCE_AUTH, options.Nonce);
        }

        [Fact]
        public void Given_APendingDeauthorizeProposal_When_BlockOptionsArePrepared_Then_CoinbaseIsTargetAndNonceIsDrop()
        {
            var store = new InMemoryCliqueProposalStore();
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC, Target);
            store.SetProposal(Target.GetPublicAddress(), authorize: false);
            var strategy = new CliqueBlockProductionStrategy(new ChainConfig(), engine, proposalStore: store);

            var options = strategy.PrepareBlockOptions(1, null);

            Assert.True(options.Coinbase.IsTheSameAddress(Target.GetPublicAddress()));
            Assert.Equal(CliqueEngine.NONCE_DROP, options.Nonce);
        }

        [Fact]
        public void Given_NoPendingProposal_When_BlockOptionsArePrepared_Then_CoinbaseIsZeroAddressNotTheSigner()
        {
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            var strategy = new CliqueBlockProductionStrategy(new ChainConfig(), engine, proposalStore: new InMemoryCliqueProposalStore());

            var options = strategy.PrepareBlockOptions(1, null);

            Assert.True(options.Coinbase.IsTheSameAddress(AddressUtil.ZERO_ADDRESS));
            Assert.False(options.Coinbase.IsTheSameAddress(KeyA.GetPublicAddress()));
            Assert.Equal(new byte[8], options.Nonce);
        }

        [Fact]
        public void Given_AProposalToAuthorizeAnAlreadyAuthorizedSigner_When_BlockOptionsArePrepared_Then_TheProposalIsSkipped()
        {
            var store = new InMemoryCliqueProposalStore();
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            store.SetProposal(KeyB.GetPublicAddress(), authorize: true);
            var strategy = new CliqueBlockProductionStrategy(new ChainConfig(), engine, proposalStore: store);

            var options = strategy.PrepareBlockOptions(1, null);

            Assert.True(options.Coinbase.IsTheSameAddress(AddressUtil.ZERO_ADDRESS));
        }

        [Fact]
        public void Given_VotesBelowRequiredVotes_When_TheLastNeededVoteIsMissing_Then_TheSignerSetIsUnchanged()
        {
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            Assert.Equal(2, engine.CurrentSnapshot.RequiredVotes);

            ApplyVoteBlock(engine, KeyA, 1, Target.GetPublicAddress(), authorize: true);

            Assert.False(engine.CurrentSnapshot.IsAuthorized(Target.GetPublicAddress()));
        }

        [Fact]
        public void Given_VotesReachingRequiredVotes_When_TheMajorityCasts_Then_TheTargetIsAddedAndItsVotesAreDiscarded()
        {
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            Assert.Equal(2, engine.CurrentSnapshot.RequiredVotes);

            ApplyVoteBlock(engine, KeyA, 1, Target.GetPublicAddress(), authorize: true);
            ApplyVoteBlock(engine, KeyB, 2, Target.GetPublicAddress(), authorize: true);

            Assert.True(engine.CurrentSnapshot.IsAuthorized(Target.GetPublicAddress()));
            Assert.Empty(engine.CurrentSnapshot.Votes);
        }

        [Fact]
        public void Given_APendingProposal_When_TheBlockIsACheckpoint_Then_NoVoteIsCastRegardlessOfTally()
        {
            var store = new InMemoryCliqueProposalStore();
            store.SetProposal(Target.GetPublicAddress(), authorize: true);
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            var strategy = new CliqueBlockProductionStrategy(
                new ChainConfig(), engine, proposalStore: store);

            var options = strategy.PrepareBlockOptions(engine.Config.EpochLength, null);

            Assert.True(options.Coinbase.IsTheSameAddress(AddressUtil.ZERO_ADDRESS));
        }

        [Fact]
        public void Given_ASignerSetThatGrewByOne_When_TheRecentSignersLimitIsRecalculated_Then_ItReflectsTheNewCount()
        {
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            Assert.Equal(2, engine.Config.CalculateRecentSignersLimit(engine.CurrentSnapshot.TotalSigners));

            ApplyVoteBlock(engine, KeyA, 1, Target.GetPublicAddress(), authorize: true);
            ApplyVoteBlock(engine, KeyB, 2, Target.GetPublicAddress(), authorize: true);

            Assert.Equal(4, engine.CurrentSnapshot.TotalSigners);
            Assert.Equal(3, engine.Config.CalculateRecentSignersLimit(engine.CurrentSnapshot.TotalSigners));
        }

        [Fact]
        public void Given_ACliqueBlockWhoseCoinbaseCarriesAVoteTarget_When_TheFeeRecipientIsResolved_Then_ItIsTheSealerNotTheTarget()
        {
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);
            var strategy = new CliqueBlockProductionStrategy(new ChainConfig(), engine);

            var header = BuildSealedHeader(
                KeyA, 1, engine.GetDifficulty(1, KeyA.GetPublicAddress()),
                Target.GetPublicAddress(), CliqueEngine.NONCE_AUTH);

            var feeRecipient = strategy.ResolveFeeRecipient(header);

            Assert.True(feeRecipient.IsTheSameAddress(KeyA.GetPublicAddress()));
            Assert.False(feeRecipient.IsTheSameAddress(Target.GetPublicAddress()));
        }

        private static readonly EthECKey SenderKey = new EthECKey("0x4c0883a69102937d6231471b5dbb6204fe5129617082792ae468d01a3f362318");
        private const string RecipientAddress = "0x3C44CdDdB6a900fa2b585dd299e03d12FA4293BC";
        private const long BaseFee = 7;
        private static readonly LegacyTransactionSigner TxSigner = new();

        private static ISignedTransaction PriorityFeeTransfer(BigInteger chainId, BigInteger gasPrice) =>
            TransactionFactory.CreateTransaction(
                TxSigner.SignTransaction(SenderKey.GetPrivateKeyAsBytes(), chainId, RecipientAddress, 100, 0, gasPrice, 21_000, ""));

        [Fact]
        public async Task Given_ACliqueBlockWhoseCoinbaseCarriesAVoteTarget_When_TheBlockIsExecutedWithTheCliqueAuthorResolver_Then_TheSealerIsCreditedNotTheTarget()
        {
            var chainId = new BigInteger(1337);
            var engine = EngineWithSigners(KeyA, KeyA, KeyB, KeyC);

            var stateStore = new InMemoryStateStore();
            await SystemContractPredeploys.ApplyGenesisAllocationAsync(stateStore, HardforkName.Amsterdam);
            await stateStore.SaveAccountAsync(SenderKey.GetPublicAddress(), new Account { Balance = 1_000_000_000_000_000, Nonce = 0 });

            var blockStore = new InMemoryBlockStore();
            var chainConfig = new ChainConfig
            {
                ChainId = chainId,
                BlockGasLimit = 30_000_000,
                BaseFee = BaseFee,
                Hardfork = nameof(HardforkName.Amsterdam)
            };
            var trieNodeStore = new InMemoryContentNodeStore();
            var stateRootCalculator = new IncrementalStateRootCalculator(stateStore, trieNodeStore);

            var blockExecutor = new BlockExecutor(
                stateStore,
                blockStore,
                new FixedChainActivations(HardforkName.Amsterdam),
                chainConfigFactory: _ => chainConfig,
                hardforkConfigFactory: _ => chainConfig.GetHardforkConfig(),
                stateRootCalculator: stateRootCalculator,
                rewardPolicy: NoRewardPolicy.Instance,
                trieNodeStore: trieNodeStore,
                authorResolver: h => engine.RecoverSigner(h) ?? h.Coinbase);

            var header = BuildSealedHeaderWithFullFields(
                KeyA, 1, engine.GetDifficulty(1, KeyA.GetPublicAddress()),
                Target.GetPublicAddress(), CliqueEngine.NONCE_AUTH,
                timestamp: 1_700_000_000, gasLimit: 30_000_000, baseFee: BaseFee);

            var tx = new TxEntry(PriorityFeeTransfer(chainId, gasPrice: BaseFee + 3));
            var result = await blockExecutor.ExecuteAsync(
                header, new[] { tx }, uncles: null, withdrawals: null, options: new BlockExecutionOptions());

            Assert.Null(result.Exception);

            var sealerEntry = result.BlockAccessList?.FirstOrDefault(
                a => string.Equals(a.Address, KeyA.GetPublicAddress(), StringComparison.OrdinalIgnoreCase));
            var targetEntry = result.BlockAccessList?.FirstOrDefault(
                a => string.Equals(a.Address, Target.GetPublicAddress(), StringComparison.OrdinalIgnoreCase));

            Assert.NotNull(sealerEntry);
            var sealerChange = Assert.Single(sealerEntry.BalanceChanges);
            Assert.Equal(new EvmUInt256(21_000 * 3), sealerChange.PostBalance);
            Assert.True(targetEntry == null || targetEntry.BalanceChanges.Count == 0);
        }
    }
}
