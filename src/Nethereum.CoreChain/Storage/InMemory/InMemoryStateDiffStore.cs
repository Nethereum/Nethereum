using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.Model;
using Nethereum.Util;

namespace Nethereum.CoreChain.Storage.InMemory
{
    public class InMemoryStateDiffStore : IStateDiffStore
    {
        private readonly SortedDictionary<(string Address, BigInteger BlockNumber), Account> _accountDiffs = new();
        private readonly SortedDictionary<(string Address, BigInteger Slot, BigInteger BlockNumber), byte[]> _storageDiffs = new();
        private readonly SortedDictionary<BigInteger, List<BlockIndexEntry>> _blockIndex = new();
        private BigInteger? _oldestBlock;
        private BigInteger? _newestBlock;
        private readonly object _lock = new();

        public Task SaveBlockDiffAsync(BlockStateDiff diff)
        {
            lock (_lock)
            {
                var indexEntries = new List<BlockIndexEntry>();

                foreach (var entry in diff.AccountDiffs)
                {
                    var normalizedAddress = NormalizeAddress(entry.Address);
                    _accountDiffs[(normalizedAddress, diff.BlockNumber)] = entry.PreValue;
                    indexEntries.Add(new BlockIndexEntry { Type = DiffType.Account, Address = normalizedAddress });
                }

                foreach (var entry in diff.StorageDiffs)
                {
                    var normalizedAddress = NormalizeAddress(entry.Address);
                    _storageDiffs[(normalizedAddress, entry.Slot, diff.BlockNumber)] = entry.PreValue;
                    indexEntries.Add(new BlockIndexEntry { Type = DiffType.Storage, Address = normalizedAddress, Slot = entry.Slot });
                }

                _blockIndex[diff.BlockNumber] = indexEntries;

                if (_oldestBlock == null || diff.BlockNumber < _oldestBlock)
                    _oldestBlock = diff.BlockNumber;
                if (_newestBlock == null || diff.BlockNumber > _newestBlock)
                    _newestBlock = diff.BlockNumber;
            }

            return Task.CompletedTask;
        }

        public Task<(bool Found, Account PreValue)> GetFirstAccountPreValueAfterBlockAsync(string address, BigInteger blockNumber)
        {
            var normalizedAddress = NormalizeAddress(address);
            var searchFrom = blockNumber + 1;

            lock (_lock)
            {
                foreach (var kvp in _accountDiffs)
                {
                    if (kvp.Key.Address != normalizedAddress) continue;
                    if (kvp.Key.BlockNumber < searchFrom) continue;
                    return Task.FromResult((true, kvp.Value));
                }
            }

            return Task.FromResult((false, (Account)null));
        }

        public Task<(bool Found, byte[] PreValue)> GetFirstStoragePreValueAfterBlockAsync(string address, BigInteger slot, BigInteger blockNumber)
        {
            var normalizedAddress = NormalizeAddress(address);
            var searchFrom = blockNumber + 1;

            lock (_lock)
            {
                foreach (var kvp in _storageDiffs)
                {
                    if (kvp.Key.Address != normalizedAddress || kvp.Key.Slot != slot) continue;
                    if (kvp.Key.BlockNumber < searchFrom) continue;
                    return Task.FromResult((true, kvp.Value));
                }
            }

            return Task.FromResult((false, (byte[])null));
        }

        public Task DeleteDiffsAboveBlockAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                var blocksToRemove = _blockIndex.Keys.Where(b => b > blockNumber).ToList();
                foreach (var block in blocksToRemove)
                {
                    if (_blockIndex.TryGetValue(block, out var entries))
                    {
                        foreach (var entry in entries)
                        {
                            if (entry.Type == DiffType.Account)
                                _accountDiffs.Remove((entry.Address, block));
                            else
                                _storageDiffs.Remove((entry.Address, entry.Slot, block));
                        }
                    }
                    _blockIndex.Remove(block);
                }

                UpdateBounds();
            }

            return Task.CompletedTask;
        }

        public Task DeleteDiffsBelowBlockAsync(BigInteger blockNumber)
        {
            lock (_lock)
            {
                var blocksToRemove = _blockIndex.Keys.Where(b => b < blockNumber).ToList();
                foreach (var block in blocksToRemove)
                {
                    if (_blockIndex.TryGetValue(block, out var entries))
                    {
                        foreach (var entry in entries)
                        {
                            if (entry.Type == DiffType.Account)
                                _accountDiffs.Remove((entry.Address, block));
                            else
                                _storageDiffs.Remove((entry.Address, entry.Slot, block));
                        }
                    }
                    _blockIndex.Remove(block);
                }

                UpdateBounds();
            }

            return Task.CompletedTask;
        }

        public Task<BigInteger?> GetOldestDiffBlockAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_oldestBlock);
            }
        }

        public Task<BigInteger?> GetNewestDiffBlockAsync()
        {
            lock (_lock)
            {
                return Task.FromResult(_newestBlock);
            }
        }

        private void UpdateBounds()
        {
            if (_blockIndex.Count == 0)
            {
                _oldestBlock = null;
                _newestBlock = null;
            }
            else
            {
                _oldestBlock = _blockIndex.Keys.First();
                _newestBlock = _blockIndex.Keys.Last();
            }
        }

        private static string NormalizeAddress(string address)
        {
            return AddressUtil.Current.ConvertToValid20ByteAddress(address).ToLowerInvariant();
        }

        private enum DiffType : byte
        {
            Account = 1,
            Storage = 2
        }

        private class BlockIndexEntry
        {
            public DiffType Type { get; set; }
            public string Address { get; set; }
            public BigInteger Slot { get; set; }
        }
    }
}
