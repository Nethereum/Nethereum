using System.Collections.Generic;
using Nethereum.Util;

namespace Nethereum.Model
{
    /// <summary>
    /// EIP-7928: every state change a block made, grouped by account. The
    /// block's header carries the hash of this list; the list itself is not
    /// part of the block body.
    ///
    /// <para>Changes are stamped with the index of the transaction that made
    /// them, and record the value AFTER the change. An account is listed for
    /// being read as well as written — including as the target of a call that
    /// reverts.</para>
    /// </summary>
    public class AccountChanges
    {
        public string Address { get; set; }
        public List<SlotChanges> StorageChanges { get; set; }
        public List<EvmUInt256> StorageReads { get; set; }
        public List<BalanceChange> BalanceChanges { get; set; }
        public List<NonceChange> NonceChanges { get; set; }
        public List<CodeChange> CodeChanges { get; set; }

        public AccountChanges()
        {
            StorageChanges = new List<SlotChanges>();
            StorageReads = new List<EvmUInt256>();
            BalanceChanges = new List<BalanceChange>();
            NonceChanges = new List<NonceChange>();
            CodeChanges = new List<CodeChange>();
        }

        public AccountChanges(string address) : this()
        {
            Address = address;
        }
    }

    public class SlotChanges
    {
        public EvmUInt256 Slot { get; set; }
        public List<StorageChange> Changes { get; set; }

        public SlotChanges()
        {
            Changes = new List<StorageChange>();
        }

        public SlotChanges(EvmUInt256 slot) : this()
        {
            Slot = slot;
        }
    }

    public class StorageChange
    {
        public ulong BlockAccessIndex { get; set; }
        public EvmUInt256 PostValue { get; set; }

        public StorageChange() { }

        public StorageChange(ulong blockAccessIndex, EvmUInt256 postValue)
        {
            BlockAccessIndex = blockAccessIndex;
            PostValue = postValue;
        }
    }

    public class BalanceChange
    {
        public ulong BlockAccessIndex { get; set; }
        public EvmUInt256 PostBalance { get; set; }

        public BalanceChange() { }

        public BalanceChange(ulong blockAccessIndex, EvmUInt256 postBalance)
        {
            BlockAccessIndex = blockAccessIndex;
            PostBalance = postBalance;
        }
    }

    public class NonceChange
    {
        public ulong BlockAccessIndex { get; set; }
        public ulong NewNonce { get; set; }

        public NonceChange() { }

        public NonceChange(ulong blockAccessIndex, ulong newNonce)
        {
            BlockAccessIndex = blockAccessIndex;
            NewNonce = newNonce;
        }
    }

    public class CodeChange
    {
        public ulong BlockAccessIndex { get; set; }
        public byte[] NewCode { get; set; }

        public CodeChange() { }

        public CodeChange(ulong blockAccessIndex, byte[] newCode)
        {
            BlockAccessIndex = blockAccessIndex;
            NewCode = newCode;
        }
    }
}
