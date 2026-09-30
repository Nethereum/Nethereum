using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.Util;
using Nethereum.Util.HashProviders;

namespace Nethereum.DevP2P.Sync.Snap.CatchUp
{
    public sealed class BlockAccessListApplier : IBlockAccessListApplier
    {
        private static readonly Sha3Keccack Keccak = Sha3Keccack.Current;
        private static readonly Sha3KeccackHashProvider SlotHashProvider = new Sha3KeccackHashProvider();

        private readonly ISnapFlatStateWriter _flat;
        private readonly IStateStore _codeStore;

        public BlockAccessListApplier(ISnapFlatStateWriter flat, IStateStore codeStore)
        {
            _flat = flat ?? throw new ArgumentNullException(nameof(flat));
            _codeStore = codeStore ?? throw new ArgumentNullException(nameof(codeStore));
        }

        public async Task ApplyAsync(
            IReadOnlyList<AccountChanges> blockAccessList, ISnapTaskFrontier frontier, CancellationToken ct = default)
        {
            if (blockAccessList == null) throw new ArgumentNullException(nameof(blockAccessList));
            if (frontier == null) throw new ArgumentNullException(nameof(frontier));

            foreach (var account in blockAccessList)
            {
                ct.ThrowIfCancellationRequested();
                var accountHash = Keccak.CalculateHash(account.Address.HexToByteArray());
                await ApplyStorageAsync(accountHash, account, frontier).ConfigureAwait(false);
                await ApplyAccountAsync(accountHash, account, frontier).ConfigureAwait(false);
            }
        }

        private async Task ApplyStorageAsync(byte[] accountHash, AccountChanges account, ISnapTaskFrontier frontier)
        {
            foreach (var slot in account.StorageChanges)
            {
                if (slot.Changes == null || slot.Changes.Count == 0) continue;

                var slotHash = HashSlot(slot.Slot);
                if (!frontier.IsStorageFetched(accountHash, slotHash)) continue;

                var postValue = LastByIndex(slot.Changes, c => c.BlockAccessIndex).PostValue;
                await _flat.SaveStorageByHashAsync(accountHash, slotHash, postValue.ToBytesForRLPEncoding()).ConfigureAwait(false);
            }
        }

        private async Task ApplyAccountAsync(byte[] accountHash, AccountChanges changes, ISnapTaskFrontier frontier)
        {
            if (!frontier.IsAccountFetched(accountHash)) return;

            var existing = await _flat.GetAccountByHashAsync(accountHash).ConfigureAwait(false);
            var isNew = existing == null;
            var account = existing ?? new Account();

            if (changes.BalanceChanges.Count > 0)
                account.Balance = LastByIndex(changes.BalanceChanges, c => c.BlockAccessIndex).PostBalance;
            if (changes.NonceChanges.Count > 0)
                account.Nonce = new EvmUInt256(LastByIndex(changes.NonceChanges, c => c.BlockAccessIndex).NewNonce);
            if (changes.CodeChanges.Count > 0)
            {
                var code = LastByIndex(changes.CodeChanges, c => c.BlockAccessIndex).NewCode;
                if (code != null && code.Length > 0)
                {
                    var codeHash = Keccak.CalculateHash(code);
                    await _codeStore.SaveCodeAsync(codeHash, code).ConfigureAwait(false);
                    account.CodeHash = codeHash;
                }
                else
                {
                    account.CodeHash = DefaultValues.EMPTY_DATA_HASH;
                }
            }

            var empty = IsEmpty(account);

            if (empty)
            {
                if (!isNew) await _flat.DeleteAccountByHashAsync(accountHash).ConfigureAwait(false);
            }
            else
            {
                await _flat.SaveAccountByHashAsync(accountHash, account).ConfigureAwait(false);
            }
        }

        private static bool IsEmpty(Account account)
            => account.Balance.IsZero
               && account.Nonce.IsZero
               && ByteUtil.AreEqual(account.CodeHash, DefaultValues.EMPTY_DATA_HASH);

        private static byte[] HashSlot(EvmUInt256 slot)
            => AccountStorage.EncodeKeyForStorage(slot.ToBigEndian(), SlotHashProvider);

        private static T LastByIndex<T>(List<T> items, Func<T, ulong> index)
        {
            var best = items[0];
            var bestIndex = index(best);
            for (var i = 1; i < items.Count; i++)
            {
                var current = index(items[i]);
                if (current >= bestIndex) { best = items[i]; bestIndex = current; }
            }
            return best;
        }
    }
}
