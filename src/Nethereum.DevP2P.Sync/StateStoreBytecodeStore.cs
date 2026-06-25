using System;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevP2P.Sync
{
    /// <summary>
    /// Synchronous <see cref="IBytecodeStore"/> adapter over the async
    /// <see cref="IStateStore.GetCodeAsync"/>. Required because the snap/1
    /// request-handler pipeline (<see cref="PatriciaSnapRequestHandler"/>,
    /// <see cref="Snap1Handler"/>) is synchronous by design — every lookup
    /// happens inside an already-async response handler that buffers a single
    /// response per request, and adding a second async hop deep in the per-
    /// request path would multiply allocations on the serving hot path.
    ///
    /// <para>
    /// Mainnet has &gt;3M bytecodes and the GetByteCodes response budget is
    /// 2 MB; a typical request batches dozens of code lookups. Each
    /// <c>GetAwaiter().GetResult()</c> call on
    /// <see cref="IStateStore.GetCodeAsync"/> reads a single value from the
    /// bytecode column family and returns immediately — no I/O blocking
    /// concerns on the served side.
    /// </para>
    /// </summary>
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
