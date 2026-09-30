using System;
using Nethereum.Util;

namespace Nethereum.EVM.Gas.Opcodes.Rules
{
    public readonly struct AccountExistenceFacts
    {
        private readonly EvmUInt256 _balance;
        private readonly byte[] _code;
        private readonly EvmUInt256 _nonce;
        private readonly bool _hasAnAccountRecord;
        private readonly bool _isResolved;

        private AccountExistenceFacts(EvmUInt256 balance, byte[] code, EvmUInt256 nonce, bool hasAnAccountRecord)
        {
            _balance = balance;
            _code = code;
            _nonce = nonce;
            _hasAnAccountRecord = hasAnAccountRecord;
            _isResolved = true;
        }

        public static AccountExistenceFacts Resolved(
            EvmUInt256 balance, byte[] code, EvmUInt256 nonce, bool hasAnAccountRecord) =>
            new AccountExistenceFacts(balance, code, nonce, hasAnAccountRecord);

        /// <summary>
        /// EIP-161: an account "is considered empty when it has no code and
        /// zero nonce and zero balance".
        /// </summary>
        public bool IsEmptyPerEip161
        {
            get
            {
                RequireResolved();
                return _balance == 0 && (_code == null || _code.Length == 0) && _nonce == 0;
            }
        }

        /// <summary>
        /// Existence as the forks before EIP-161 judge it — EIP-161 states the
        /// rule it replaces in its own words: "<c>CALL</c> and <c>SUICIDE</c>
        /// would charge 25,000 gas when the destination is non-existent". A
        /// record that exists but is empty still counts as existing.
        /// </summary>
        public bool HasAnAccountRecord
        {
            get { RequireResolved(); return _hasAnAccountRecord; }
        }

        private void RequireResolved()
        {
            if (!_isResolved)
                throw new InvalidOperationException(
                    "An account is judged only after Resolved has read its fields.");
        }
    }
}
