using System.Threading.Tasks;

namespace Nethereum.CoreChain
{
    public interface IIncrementalStateRootCalculator
    {
        Task<byte[]> ComputeStateRootAsync();

        /// <summary>
        /// First-compute init path that lazy-loads the existing trie from
        /// <paramref name="previousStateRoot"/> instead of walking every flat
        /// account. Pass the parent header's <c>StateRoot</c> on the first
        /// compute of a freshly-constructed calculator; subsequent calls are
        /// equivalent to <see cref="ComputeStateRootAsync()"/> (the parameter
        /// is ignored once initialised). A <c>null</c> or empty value falls
        /// back to the full-state walk — required for genesis / cold AppChain
        /// init where no prior root exists.
        /// </summary>
        Task<byte[]> ComputeStateRootAsync(byte[] previousStateRoot);

        Task<byte[]> ComputeFullStateRootAsync();
    }
}
