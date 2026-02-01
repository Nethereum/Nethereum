using System;
using System.Collections.Generic;
using System.Numerics;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AccountAbstraction.Bundler.Validation.ERC7562
{
    public class AssociatedStorageCalculator
    {
        private readonly Dictionary<BigInteger, KeccakPreimage> _keccakPreimages = new();
        private readonly Dictionary<string, HashSet<BigInteger>> _associatedSlots = new();
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public void TrackKeccak(byte[] input, byte[] output)
        {
            if (input == null || output == null || output.Length != 32) return;

            var resultHash = new BigInteger(output, true, true);
            var preimage = ParseKeccakInput(input);
            if (preimage != null)
            {
                _keccakPreimages[resultHash] = preimage;
            }
        }

        public void TrackKeccakFromHash(byte[] input, BigInteger resultHash)
        {
            if (input == null) return;

            var preimage = ParseKeccakInput(input);
            if (preimage != null)
            {
                _keccakPreimages[resultHash] = preimage;
            }
        }

        public bool IsAssociatedSlot(string contractAddress, BigInteger slot, string senderAddress)
        {
            var normalizedContract = contractAddress?.ToLowerInvariant() ?? "";
            var normalizedSender = senderAddress?.ToLowerInvariant() ?? "";

            if (_associatedSlots.TryGetValue(normalizedContract, out var slots) && slots.Contains(slot))
            {
                return true;
            }

            var association = CheckSlotAssociation(slot, normalizedSender);
            if (association != SlotAssociationType.None)
            {
                TrackAssociatedSlot(normalizedContract, slot);
                return true;
            }

            return false;
        }

        public void RegisterSenderSlot(string senderAddress, BigInteger baseSlot)
        {
            var normalized = senderAddress?.ToLowerInvariant() ?? "";
            if (string.IsNullOrEmpty(normalized)) return;

            var addressPadded = new byte[32];
            var addressBytes = normalized.HexToByteArray();
            Array.Copy(addressBytes, 0, addressPadded, 12, 20);

            for (int i = 0; i < 256; i++)
            {
                var slotBytes = baseSlot.ToByteArray(true, true);
                var slotPadded = new byte[32];
                Array.Copy(slotBytes, 0, slotPadded, 32 - slotBytes.Length, slotBytes.Length);

                var combined = new byte[64];
                Array.Copy(addressPadded, 0, combined, 0, 32);
                Array.Copy(slotPadded, 0, combined, 32, 32);

                var hash = _keccak.CalculateHash(combined);
                var associatedSlot = new BigInteger(hash, true, true);

                TrackAssociatedSlot(normalized, associatedSlot);

                if (i > 0) break;
            }
        }

        public void TrackAssociatedSlot(string contractAddress, BigInteger slot)
        {
            var normalized = contractAddress?.ToLowerInvariant() ?? "";
            if (!_associatedSlots.ContainsKey(normalized))
            {
                _associatedSlots[normalized] = new HashSet<BigInteger>();
            }
            _associatedSlots[normalized].Add(slot);
        }

        public IReadOnlyDictionary<string, HashSet<BigInteger>> GetAssociatedSlots() => _associatedSlots;

        private SlotAssociationType CheckSlotAssociation(BigInteger slot, string senderAddress)
        {
            if (!_keccakPreimages.TryGetValue(slot, out var preimage))
            {
                return SlotAssociationType.None;
            }

            if (preimage.ContainsAddress && preimage.Address != null &&
                preimage.Address.Equals(senderAddress, StringComparison.OrdinalIgnoreCase))
            {
                return SlotAssociationType.Mapping;
            }

            if (preimage.BaseSlot.HasValue)
            {
                var baseAssociation = CheckSlotAssociation(preimage.BaseSlot.Value, senderAddress);
                if (baseAssociation != SlotAssociationType.None)
                {
                    return SlotAssociationType.NestedMapping;
                }
            }

            return SlotAssociationType.None;
        }

        private KeccakPreimage? ParseKeccakInput(byte[] input)
        {
            if (input == null) return null;

            if (input.Length == 64)
            {
                return ParseMappingInput(input);
            }

            if (input.Length == 32)
            {
                return new KeccakPreimage
                {
                    InputType = KeccakInputType.ArraySlot,
                    BaseSlot = new BigInteger(input, true, true)
                };
            }

            return null;
        }

        private KeccakPreimage? ParseMappingInput(byte[] input)
        {
            var first32 = new byte[32];
            var second32 = new byte[32];
            Array.Copy(input, 0, first32, 0, 32);
            Array.Copy(input, 32, second32, 0, 32);

            bool first12Zero = true;
            for (int i = 0; i < 12; i++)
            {
                if (first32[i] != 0) { first12Zero = false; break; }
            }

            if (first12Zero)
            {
                var addressBytes = new byte[20];
                Array.Copy(first32, 12, addressBytes, 0, 20);
                var address = "0x" + addressBytes.ToHex();

                return new KeccakPreimage
                {
                    InputType = KeccakInputType.AddressMapping,
                    ContainsAddress = true,
                    Address = address.ToLowerInvariant(),
                    BaseSlot = new BigInteger(second32, true, true)
                };
            }

            var key = new BigInteger(first32, true, true);
            var slot = new BigInteger(second32, true, true);

            return new KeccakPreimage
            {
                InputType = KeccakInputType.ValueMapping,
                Key = key,
                BaseSlot = slot
            };
        }
    }

    public class KeccakPreimage
    {
        public KeccakInputType InputType { get; set; }
        public bool ContainsAddress { get; set; }
        public string? Address { get; set; }
        public BigInteger? Key { get; set; }
        public BigInteger? BaseSlot { get; set; }
    }

    public enum KeccakInputType
    {
        Unknown,
        AddressMapping,
        ValueMapping,
        ArraySlot,
        NestedMapping
    }

    public enum SlotAssociationType
    {
        None,
        Mapping,
        NestedMapping,
        Array
    }
}
