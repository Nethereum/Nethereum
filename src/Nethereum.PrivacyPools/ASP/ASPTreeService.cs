using System.Collections.Generic;
using System.Linq;
using System.Numerics;
using System.Threading.Tasks;
using Nethereum.PrivacyPools.Entrypoint;

namespace Nethereum.PrivacyPools
{
    public class ASPTreeService
    {
        private readonly EntrypointService _entrypoint;
        private PoseidonMerkleTree _tree;

        public PoseidonMerkleTree Tree => _tree;
        public BigInteger Root => _tree.RootAsBigInteger;
        public int Size => _tree.Size;
        public string LatestIpfsCid { get; private set; }

        public ASPTreeService(EntrypointService entrypoint)
        {
            _entrypoint = entrypoint;
            _tree = new PoseidonMerkleTree();
        }

        public void BuildFromLabels(IEnumerable<BigInteger> labels)
        {
            _tree = new PoseidonMerkleTree();
            _tree.InsertCommitments(labels);
        }

        public void BuildFromDeposits(IEnumerable<PoolDepositEventData> deposits)
        {
            var labels = deposits.Select(d => d.Label).ToList();
            BuildFromLabels(labels);
        }

        public void InsertLabel(BigInteger label)
        {
            _tree.InsertCommitment(label);
        }

        public (BigInteger[] Siblings, int Index) GenerateProof(BigInteger label)
        {
            var leafIndex = FindLabelIndex(label);
            var proof = _tree.GenerateInclusionProof(leafIndex);
            var siblings = _tree.GetProofSiblings(proof);
            var padded = PadSiblings(siblings, PrivacyPoolConstants.MAX_TREE_DEPTH);
            return (padded, leafIndex);
        }

        public async Task<BigInteger> PublishRootAsync(string ipfsCid)
        {
            var receipt = await _entrypoint.UpdateRootRequestAndWaitForReceiptAsync(
                Root, ipfsCid);
            if (receipt.HasErrors() == true)
                throw new System.Exception("ASP root update failed");
            LatestIpfsCid = ipfsCid;
            return Root;
        }

        public async Task<BigInteger> GetOnChainLatestRootAsync()
        {
            return await _entrypoint.LatestRootQueryAsync();
        }

        public async Task<bool> IsRootPublishedAsync()
        {
            var onChainRoot = await GetOnChainLatestRootAsync();
            return onChainRoot == Root;
        }

        public string Export()
        {
            return _tree.Export();
        }

        public void Import(string json)
        {
            _tree = PoseidonMerkleTree.Import(json);
        }

        private int FindLabelIndex(BigInteger label)
        {
            for (int i = 0; i < _tree.Size; i++)
            {
                var proof = _tree.GenerateInclusionProof(i);
                if (_tree.VerifyInclusionProof(proof, label))
                    return i;
            }
            throw new System.ArgumentException($"Label not found in ASP tree");
        }

        private static BigInteger[] PadSiblings(BigInteger[] siblings, int targetLength)
        {
            var padded = new BigInteger[targetLength];
            for (int i = 0; i < siblings.Length && i < targetLength; i++)
                padded[i] = siblings[i];
            return padded;
        }
    }
}
