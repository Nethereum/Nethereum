using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Nethereum.RLP;
using Nethereum.Util;

namespace Nethereum.EVM
{
    public class AccessListTracker
    {
        private readonly Dictionary<string, HashSet<EvmUInt256>> _accessedStorage = new();
        private readonly HashSet<string> _accessedAddresses = new();
        private readonly string _fromAddress;
        private readonly string _toAddress;

        public bool IsEnabled { get; }

        public AccessListTracker(string fromAddress, string toAddress, bool enabled = false)
        {
            _fromAddress = fromAddress?.ToLowerInvariant();
            _toAddress = toAddress?.ToLowerInvariant();
            IsEnabled = enabled;
        }

        public void RecordAddressAccess(string address)
        {
            if (!IsEnabled || string.IsNullOrEmpty(address)) return;

            var normalizedAddress = address.ToLowerInvariant();
            if (normalizedAddress == _fromAddress || normalizedAddress == _toAddress)
                return;

            _accessedAddresses.Add(normalizedAddress);
        }

        public void RecordStorageAccess(string address, EvmUInt256 slot)
        {
            if (!IsEnabled || string.IsNullOrEmpty(address)) return;

            var normalizedAddress = address.ToLowerInvariant();

            if (!_accessedStorage.TryGetValue(normalizedAddress, out var slots))
            {
                slots = new HashSet<EvmUInt256>();
                _accessedStorage[normalizedAddress] = slots;
            }

            slots.Add(slot);
            _accessedAddresses.Add(normalizedAddress);
        }

        public List<AccessListItem> GetAccessList()
        {
            var result = new List<AccessListItem>();

            var sortedAddresses = new List<string>(_accessedAddresses);
            sortedAddresses.Sort(System.StringComparer.Ordinal);

            foreach (var address in sortedAddresses)
            {
                var storageKeys = new List<byte[]>();

                if (_accessedStorage.TryGetValue(address, out var slots))
                {
                    var sortedSlots = new List<EvmUInt256>(slots);
                    sortedSlots.Sort();

                    foreach (var slot in sortedSlots)
                    {
                        var slotBytes = slot.ToBigEndian();
                        storageKeys.Add(slotBytes);
                    }
                }

                result.Add(new AccessListItem(address.EnsureHexPrefix(), storageKeys));
            }

            return result;
        }

        public void Clear()
        {
            _accessedStorage.Clear();
            _accessedAddresses.Clear();
        }
    }
}
