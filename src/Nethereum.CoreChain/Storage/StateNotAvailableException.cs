using System;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Storage
{
    public class StateNotAvailableException : Exception
    {
        public byte[] StateRoot { get; }

        public StateNotAvailableException(byte[] stateRoot)
            : base($"State not available: trie root 0x{(stateRoot == null ? "(null)" : stateRoot.ToHex())} is not retained by this node")
        {
            StateRoot = stateRoot;
        }
    }
}
