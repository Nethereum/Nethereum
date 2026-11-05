using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Prefetch
{
    internal enum PrefetchStep
    {
        Completed,
        Interrupted,
        Failed
    }

    internal sealed class TransactionPrefetch
    {
        private readonly IStateStore _state;
        private readonly IBlockStore _blocks;
        private readonly ITransactionVerificationAndRecovery _verifier;

        public TransactionPrefetch(IStateStore state, IBlockStore blocks, ITransactionVerificationAndRecovery verifier)
        {
            _verifier = verifier;
            _state = state;
            _blocks = blocks;
        }

        public async Task<PrefetchStep> WarmAsync(
            PrefetchBlock block, ISignedTransaction transaction, int index, Func<bool> isStopped, string? sender)
        {
            if (sender == null) return PrefetchStep.Failed;
            if (isStopped()) return PrefetchStep.Interrupted;

            await ReadTouchedStateAsync(transaction, sender).ConfigureAwait(false);
            if (isStopped()) return PrefetchStep.Interrupted;

            await ExecuteInThrowawayLayerAsync(block, transaction, index, sender).ConfigureAwait(false);
            return PrefetchStep.Completed;
        }

        private async Task ReadTouchedStateAsync(ISignedTransaction transaction, string sender)
        {
            await _state.GetAccountAsync(EvmAddress.FromHex(sender)).ConfigureAwait(false);

            var receiver = transaction.GetReceiverAddress();
            if (!string.IsNullOrEmpty(receiver))
            {
                var account = await _state.GetAccountAsync(EvmAddress.FromHex(receiver)).ConfigureAwait(false);
                await ReadCodeOfAsync(account).ConfigureAwait(false);
            }

            await ReadAccessListAsync(transaction.GetAccessList()).ConfigureAwait(false);
        }

        private async Task ReadCodeOfAsync(Account? account)
        {
            var codeHash = account?.CodeHash;
            if (codeHash == null || codeHash.Length == 0) return;
            if (ByteUtil.AreEqual(codeHash, DefaultValues.EMPTY_DATA_HASH)) return;
            await _state.GetCodeAsync(codeHash).ConfigureAwait(false);
        }

        private async Task ReadAccessListAsync(List<AccessListItem>? accessList)
        {
            if (accessList == null) return;
            foreach (var item in accessList)
            {
                var address = EvmAddress.FromHex(item.Address);
                await _state.GetAccountAsync(address).ConfigureAwait(false);
                foreach (var key in item.StorageKeys)
                    await _state.GetStorageAsync(address, SlotOf(key)).ConfigureAwait(false);
            }
        }

        private static BigInteger SlotOf(byte[] key) => new BigInteger(key, isUnsigned: true, isBigEndian: true);

        private async Task ExecuteInThrowawayLayerAsync(
            PrefetchBlock block, ISignedTransaction transaction, int index, string sender)
        {
            var layer = new ReadOnlyStateStoreWrapper(_state);
            await AlignSenderNonceAsync(layer, sender, transaction).ConfigureAwait(false);

            var processor = new TransactionProcessor(
                layer, _blocks, block.ChainConfig, _verifier, block.HardforkConfig, block.Eip158EmptyAccountPruning);
            await processor.ExecuteTransactionAsync(transaction, block.NewContext(), index, 0, sender).ConfigureAwait(false);
        }

        private static async Task AlignSenderNonceAsync(
            ReadOnlyStateStoreWrapper layer, string sender, ISignedTransaction transaction)
        {
            var address = EvmAddress.FromHex(sender);
            var account = await layer.GetAccountAsync(address).ConfigureAwait(false) ?? new Account
            {
                Balance = 0,
                CodeHash = DefaultValues.EMPTY_DATA_HASH,
                StateRoot = DefaultValues.EMPTY_TRIE_HASH
            };
            account.Nonce = transaction.GetNonce();
            await layer.SaveAccountAsync(address, account).ConfigureAwait(false);
        }
    }
}
