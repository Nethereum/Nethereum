using System;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync.Serving
{
    public sealed class StateStoreBytecodeStore : IBytecodeStore
    {
        private readonly IStateStore _state;

        public StateStoreBytecodeStore(IStateStore state)
        {
            _state = state ?? throw new ArgumentNullException(nameof(state));
        }

        public byte[] Get(byte[] codeHash)
        {
            if (codeHash == null) return null;
            return _state.GetCodeAsync(codeHash).GetAwaiter().GetResult();
        }
    }
}
