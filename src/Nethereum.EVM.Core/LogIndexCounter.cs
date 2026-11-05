using Nethereum.Documentation;
#if !EVM_SYNC
namespace Nethereum.EVM
{
    [NethereumDocExample(DocSection.EvmSimulator, "log-index", "Block-wide log ordinal")]
    public sealed class LogIndexCounter
    {
        private long _next;

        public long Next()
        {
            return _next++;
        }
    }
}
#endif
