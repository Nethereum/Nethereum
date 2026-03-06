using System.Collections.Generic;
using System.IO;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Nethereum.Model;

namespace Nethereum.AppChain.Sync
{
    public interface IBatchWriter
    {
        Task<BatchInfo> WriteBatchAsync(
            Stream outputStream,
            BigInteger chainId,
            IEnumerable<BatchBlockData> blocks,
            CancellationToken cancellationToken = default);

        Task<BatchInfo> WriteBatchToFileAsync(
            string filePath,
            BigInteger chainId,
            IEnumerable<BatchBlockData> blocks,
            bool compress = true,
            CancellationToken cancellationToken = default);
    }
}
