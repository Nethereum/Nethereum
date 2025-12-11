using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Nethereum.Model;

namespace Nethereum.ChainStateVerification.Caching
{
    public class VerifiedStateCache : IDisposable
    {
        private readonly Dictionary<string, VerifiedAccountState> _accounts
            = new Dictionary<string, VerifiedAccountState>(StringComparer.OrdinalIgnoreCase);

        private readonly Dictionary<string, byte[]> _verifiedCode
            = new Dictionary<string, byte[]>(StringComparer.OrdinalIgnoreCase);

        private readonly ReaderWriterLockSlim _lock = new ReaderWriterLockSlim();

        private ulong _blockNumber;
        private byte[] _stateRoot;
        private bool _disposed;

        public ulong BlockNumber
        {
            get
            {
                _lock.EnterReadLock();
                try { return _blockNumber; }
                finally { _lock.ExitReadLock(); }
            }
        }

        public byte[] StateRoot
        {
            get
            {
                _lock.EnterReadLock();
                try { return _stateRoot; }
                finally { _lock.ExitReadLock(); }
            }
        }

        public void SetBlock(ulong blockNumber, byte[] stateRoot)
        {
            if (stateRoot == null) throw new ArgumentNullException(nameof(stateRoot));

            _lock.EnterWriteLock();
            try
            {
                if (_blockNumber != blockNumber || _stateRoot == null || !_stateRoot.SequenceEqual(stateRoot))
                {
                    _accounts.Clear();
                    _blockNumber = blockNumber;
                    _stateRoot = stateRoot;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetAccount(string address, out VerifiedAccountState state)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                state = null;
                return false;
            }

            _lock.EnterReadLock();
            try
            {
                return _accounts.TryGetValue(address, out state) && state.Account != null;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void SetAccount(string address, Account account)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.", nameof(address));
            if (account == null) throw new ArgumentNullException(nameof(account));

            _lock.EnterWriteLock();
            try
            {
                if (!_accounts.TryGetValue(address, out var state))
                {
                    state = new VerifiedAccountState(address);
                    _accounts[address] = state;
                }
                state.Account = account;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetCode(string address, out byte[] code)
        {
            if (string.IsNullOrWhiteSpace(address))
            {
                code = null;
                return false;
            }

            _lock.EnterReadLock();
            try
            {
                if (_verifiedCode.TryGetValue(address, out code))
                {
                    return true;
                }

                if (_accounts.TryGetValue(address, out var state) && state.CodeVerified)
                {
                    code = state.Code;
                    return true;
                }

                code = null;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void SetCode(string address, byte[] code)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.", nameof(address));

            _lock.EnterWriteLock();
            try
            {
                _verifiedCode[address] = code ?? Array.Empty<byte>();

                if (_accounts.TryGetValue(address, out var state))
                {
                    state.Code = code ?? Array.Empty<byte>();
                    state.CodeVerified = true;
                }
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public bool TryGetStorage(string address, string slotHex, out byte[] value)
        {
            if (string.IsNullOrWhiteSpace(address) || string.IsNullOrWhiteSpace(slotHex))
            {
                value = null;
                return false;
            }

            _lock.EnterReadLock();
            try
            {
                if (_accounts.TryGetValue(address, out var state))
                {
                    return state.Storage.TryGetValue(slotHex, out value);
                }
                value = null;
                return false;
            }
            finally
            {
                _lock.ExitReadLock();
            }
        }

        public void SetStorage(string address, string slotHex, byte[] value)
        {
            if (string.IsNullOrWhiteSpace(address)) throw new ArgumentException("Address is required.", nameof(address));
            if (string.IsNullOrWhiteSpace(slotHex)) throw new ArgumentException("Storage slot is required.", nameof(slotHex));

            _lock.EnterWriteLock();
            try
            {
                if (!_accounts.TryGetValue(address, out var state))
                {
                    state = new VerifiedAccountState(address);
                    _accounts[address] = state;
                }
                state.Storage[slotHex] = value ?? Array.Empty<byte>();
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Clear()
        {
            _lock.EnterWriteLock();
            try
            {
                _accounts.Clear();
                _blockNumber = 0;
                _stateRoot = null;
            }
            finally
            {
                _lock.ExitWriteLock();
            }
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed) return;

            if (disposing)
            {
                _lock?.Dispose();
            }

            _disposed = true;
        }
    }
}
