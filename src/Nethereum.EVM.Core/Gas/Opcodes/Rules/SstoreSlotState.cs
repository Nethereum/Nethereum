using System;
using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    /// <summary>
    /// A storage slot as an SSTORE gas rule sees it.
    ///
    /// <para>EIP-1283: "Storage slot's <i>original value</i>: This is the value
    /// of the storage if a reversion happens on the <i>current transaction</i>.
    /// Storage slot's <i>current value</i>: This is the value of the storage
    /// before SSTORE operation happens. Storage slot's <i>new value</i>: This is
    /// the value of the storage after SSTORE operation happens."</para>
    ///
    /// <para>A struct always has a <c>default</c>, and an unresolved one answers
    /// every question plausibly — two absent values compare equal, so a slot
    /// nobody read would report itself unchanged and warm. Resolution is
    /// therefore carried as state and every predicate refuses without it.</para>
    /// </summary>
    public readonly struct SstoreSlotState
    {
        private const int WordLength = 32;

        private readonly byte[] _original;
        private readonly byte[] _current;
        private readonly byte[] _new;
        private readonly bool _isColdAccess;
        private readonly bool _isResolved;

        private SstoreSlotState(byte[] original, byte[] current, byte[] newValue, bool isColdAccess)
        {
            _original = original;
            _current = current;
            _new = newValue;
            _isColdAccess = isColdAccess;
            _isResolved = true;
        }

        public static SstoreSlotState Resolved(byte[] original, byte[] current, byte[] newValue, bool isColdAccess)
        {
            RequireWord(original, nameof(original));
            RequireWord(current, nameof(current));
            RequireWord(newValue, nameof(newValue));

            return new SstoreSlotState(original, current, newValue, isColdAccess);
        }

        /// <summary>
        /// EIP-2929: "check if the <c>(address, storage_key)</c> pair is in
        /// <c>accessed_storage_keys</c>".
        /// </summary>
        public bool IsColdAccess
        {
            get { RequireResolved(); return _isColdAccess; }
        }

        /// <summary>EIP-8038 phrases its write component as "the new value is
        /// different from the current value".</summary>
        public bool NewValueDiffersFromCurrent
        {
            get { RequireResolved(); return !ByteUtil.AreEqual(_current, _new); }
        }

        public bool NewValueEqualsCurrent
        {
            get { RequireResolved(); return ByteUtil.AreEqual(_current, _new); }
        }

        public bool SlotStillHoldsItsTransactionStartValue
        {
            get { RequireResolved(); return ByteUtil.AreEqual(_original, _current); }
        }

        public bool OriginalValueIsZero
        {
            get { RequireResolved(); return ByteUtil.IsZero(_original); }
        }

        public bool CurrentValueIsZero
        {
            get { RequireResolved(); return ByteUtil.IsZero(_current); }
        }

        public bool NewValueIsZero
        {
            get { RequireResolved(); return ByteUtil.IsZero(_new); }
        }

        private void RequireResolved()
        {
            if (!_isResolved)
                throw new InvalidOperationException(
                    "An SSTORE slot is priced only after Resolved has read it.");
        }

        private static void RequireWord(byte[] value, string name)
        {
            if (value == null) throw new ArgumentNullException(name);
            if (value.Length != WordLength)
                throw new ArgumentException("An SSTORE slot value is a resolved 32-byte word.", name);
        }
    }
}
