using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;

namespace Nethereum.DevChain.Storage
{
    public class InMemoryBlobStore : IBlobStore
    {
        private readonly ConcurrentDictionary<BigInteger, List<BlobSidecarRecord>> _byBlock = new();
        private readonly ConcurrentDictionary<string, List<BlobSidecarRecord>> _byTxHash = new();
        private readonly ConcurrentDictionary<string, BlobSidecarRecord> _byVersionedHash = new();

        public Task StoreBlobsAsync(BigInteger blockNumber, byte[] transactionHash, List<BlobSidecarRecord> blobs)
        {
            var txKey = ToHexKey(transactionHash);

            _byBlock.AddOrUpdate(blockNumber,
                _ => new List<BlobSidecarRecord>(blobs),
                (_, existing) => { existing.AddRange(blobs); return existing; });

            _byTxHash.AddOrUpdate(txKey,
                _ => new List<BlobSidecarRecord>(blobs),
                (_, existing) => { existing.AddRange(blobs); return existing; });

            foreach (var blob in blobs)
            {
                if (blob.VersionedHash != null)
                    _byVersionedHash[ToHexKey(blob.VersionedHash)] = blob;
            }

            return Task.CompletedTask;
        }

        public Task<List<BlobSidecarRecord>> GetBlobsByBlockNumberAsync(BigInteger blockNumber)
        {
            _byBlock.TryGetValue(blockNumber, out var blobs);
            return Task.FromResult(blobs ?? new List<BlobSidecarRecord>());
        }

        public Task<List<BlobSidecarRecord>> GetBlobsByTransactionHashAsync(byte[] transactionHash)
        {
            _byTxHash.TryGetValue(ToHexKey(transactionHash), out var blobs);
            return Task.FromResult(blobs ?? new List<BlobSidecarRecord>());
        }

        public Task<BlobSidecarRecord> GetBlobByVersionedHashAsync(byte[] versionedHash)
        {
            _byVersionedHash.TryGetValue(ToHexKey(versionedHash), out var blob);
            return Task.FromResult(blob);
        }

        public Task<bool> HasBlobsForBlockAsync(BigInteger blockNumber)
        {
            return Task.FromResult(_byBlock.ContainsKey(blockNumber));
        }

        private static string ToHexKey(byte[] bytes)
        {
            if (bytes == null) return "";
            return Nethereum.Hex.HexConvertors.Extensions.HexByteConvertorExtensions.ToHex(bytes, true);
        }
    }
}
