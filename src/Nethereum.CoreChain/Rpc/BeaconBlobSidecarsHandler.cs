using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.CoreChain.Storage;
using Nethereum.Hex.HexConvertors.Extensions;

namespace Nethereum.CoreChain.Rpc
{
    public class BeaconBlobSidecarResult
    {
        public int Index { get; set; }
        public string Blob { get; set; }
        public string KzgCommitment { get; set; }
        public string KzgProof { get; set; }
    }

    public static class BeaconBlobSidecarsHandler
    {
        public static async Task<BigInteger> ResolveBlockId(string blockId, IChainNode node)
        {
            if (blockId == "head" || blockId == "finalized" || blockId == "justified")
                return await node.GetBlockNumberAsync();
            if (blockId == "genesis")
                return 0;
            return BigInteger.Parse(blockId);
        }

        public static async Task<List<BeaconBlobSidecarResult>> GetBlobSidecarsAsync(IChainNode node, string blockId)
        {
            var blockNumber = await ResolveBlockId(blockId, node);
            var records = node.BlobStore != null
                ? await node.BlobStore.GetBlobsByBlockNumberAsync(blockNumber)
                : new List<BlobSidecarRecord>();

            var results = new List<BeaconBlobSidecarResult>(records.Count);
            foreach (var r in records)
            {
                results.Add(new BeaconBlobSidecarResult
                {
                    Index = r.Index,
                    Blob = r.Blob?.ToHex(true) ?? "0x",
                    KzgCommitment = r.KzgCommitment?.ToHex(true) ?? "0x",
                    KzgProof = r.KzgProof?.ToHex(true) ?? "0x"
                });
            }
            return results;
        }
    }
}
