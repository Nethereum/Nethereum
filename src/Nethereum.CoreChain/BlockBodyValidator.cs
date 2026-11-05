using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Model.Codecs;
using Nethereum.Util;

namespace Nethereum.CoreChain
{
    public sealed class BlockBodyValidator
    {
        public const int MaximumUncles = 2;

        public const int AncestorWindow = 7;

        public const int MaximumRlpBlockSize = 8_388_608;

        private readonly IBlockStore _blocks;
        private readonly IUncleStore? _uncleStore;
        private readonly IChainActivations _activations;
        private readonly Func<HardforkName, HardforkConfig> _hardforkConfigFactory;
        private readonly IBlockRootsProvider _blockRootsProvider;

        public BlockBodyValidator(
            IBlockStore blocks,
            IUncleStore? uncleStore,
            IChainActivations activations,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory)
            : this(blocks, uncleStore, activations, hardforkConfigFactory, null)
        {
        }

        public BlockBodyValidator(
            IBlockStore blocks,
            IUncleStore? uncleStore,
            IChainActivations activations,
            Func<HardforkName, HardforkConfig> hardforkConfigFactory,
            IBlockRootsProvider? blockRootsProvider)
        {
            _blockRootsProvider = blockRootsProvider ?? PatriciaBlockRootsProvider.Instance;
            _blocks = blocks ?? throw new ArgumentNullException(nameof(blocks));
            _uncleStore = uncleStore;
            _activations = activations ?? throw new ArgumentNullException(nameof(activations));
            _hardforkConfigFactory = hardforkConfigFactory ?? throw new ArgumentNullException(nameof(hardforkConfigFactory));
        }

        public async Task<BlockValidityCheck?> ValidateAsync(
            BlockHeader header,
            IList<ISignedTransaction> transactions,
            IList<BlockHeader>? uncles,
            IList<Withdrawal>? withdrawals,
            HardforkName fork,
            SealRules seal,
            long? unixNow)
        {
            var bodyUncles = uncles ?? new List<BlockHeader>();

            if (fork >= HardforkName.Osaka
                && Rpc.BlockHeaderExtensions.CalculateFullBlockSize(
                    header, transactions, bodyUncles, withdrawals ?? new List<Withdrawal>()) > MaximumRlpBlockSize)
                return BlockValidityCheck.BlockRlpSize;

            var unclesAreAcceptable = seal == SealRules.Ethash
                ? await UnclesFollowTheOmmerRulesAsync(header, bodyUncles, fork, unixNow).ConfigureAwait(false)
                : bodyUncles.Count == 0;
            if (!unclesAreAcceptable) return BlockValidityCheck.Uncles;

            if (header.UnclesHash != null && !ByteUtil.AreEqual(header.UnclesHash, HashOfUncles(bodyUncles)))
                return BlockValidityCheck.UnclesHash;

            if (header.TransactionsHash != null
                && !ByteUtil.AreEqual(_blockRootsProvider.CalculateTransactionsRoot(transactions), header.TransactionsHash))
                return BlockValidityCheck.TransactionsRoot;

            if (header.WithdrawalsRoot == null && withdrawals is { Count: > 0 })
                return BlockValidityCheck.WithdrawalsRoot;

            if (header.WithdrawalsRoot != null
                && !ByteUtil.AreEqual(
                    _blockRootsProvider.CalculateWithdrawalsRoot(withdrawals ?? new List<Withdrawal>()),
                    header.WithdrawalsRoot))
                return BlockValidityCheck.WithdrawalsRoot;

            return null;
        }

        private byte[] HashOfUncles(IList<BlockHeader> uncles)
        {
            var encoded = new byte[uncles.Count][];
            for (var i = 0; i < uncles.Count; i++)
                encoded[i] = BlockHeaderCodecs.ForFork(ForkOf(uncles[i])).Encode(uncles[i]);
            return Sha3Keccack.Current.CalculateHash(RLP.RLP.EncodeList(encoded));
        }

        private async Task<bool> UnclesFollowTheOmmerRulesAsync(
            BlockHeader header, IList<BlockHeader> uncles, HardforkName fork, long? unixNow)
        {
            if (uncles.Count > MaximumUncles) return false;
            if (uncles.Count == 0) return true;

            var bannedUncles = new HashSet<string>();
            var ancestors = new Dictionary<string, BlockHeader>();

            var parentHash = header.ParentHash;
            for (var depth = 0; depth < AncestorWindow; depth++)
            {
                var ancestor = await _blocks.GetByHashAsync(parentHash).ConfigureAwait(false);
                if (ancestor == null) break;

                ancestors[parentHash.ToHex()] = ancestor;
                if (ancestor.UnclesHash != null && !ByteUtil.AreEqual(ancestor.UnclesHash, DefaultValues.EMPTY_UNCLES_HASH))
                {
                    var ancestorUncles = _uncleStore == null
                        ? null
                        : await _uncleStore.GetByBlockHashAsync(parentHash).ConfigureAwait(false);
                    if (ancestorUncles == null) break;
                    foreach (var ancestorUncle in ancestorUncles)
                        bannedUncles.Add(HashOf(ancestorUncle).ToHex());
                }
                parentHash = ancestor.ParentHash;
            }

            var blockHash = HashOf(header).ToHex();
            ancestors[blockHash] = header;
            bannedUncles.Add(blockHash);

            foreach (var uncle in uncles)
            {
                var uncleHash = HashOf(uncle).ToHex();
                if (!bannedUncles.Add(uncleHash)) return false;
                if (ancestors.ContainsKey(uncleHash)) return false;
                if (!ancestors.TryGetValue(uncle.ParentHash.ToHex(), out var uncleParent)
                    || ByteUtil.AreEqual(uncle.ParentHash, header.ParentHash))
                    return false;

                var uncleFork = ForkOf(uncle);
                var verdict = HeaderValidator.Validate(
                    uncle, uncleParent, uncleFork, _hardforkConfigFactory(uncleFork), SealRules.Ethash,
                    unixNow,
                    HeaderValidator.RequiresDaoExtraData(
                        _activations, (long)(ulong)uncle.BlockNumber, unchecked((ulong)uncle.Timestamp)),
                    uncle: true);
                if (verdict != null) return false;
            }
            return true;
        }

        private byte[] HashOf(BlockHeader header) => BlockHashCalculator.ForFork(header, ForkOf(header));

        private HardforkName ForkOf(BlockHeader header) =>
            _activations.ResolveAt((long)(ulong)header.BlockNumber, unchecked((ulong)header.Timestamp));
    }
}
