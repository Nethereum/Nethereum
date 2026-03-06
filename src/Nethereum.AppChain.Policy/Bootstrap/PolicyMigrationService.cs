using System;
using System.Collections.Generic;
using System.Linq;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Util;

namespace Nethereum.AppChain.Policy.Bootstrap
{
    public class PolicyMigrationService
    {
        private readonly Sha3Keccack _keccak = new Sha3Keccack();

        public byte[] ComputeMerkleRoot(IEnumerable<string> addresses)
        {
            var leaves = addresses
                .Select(a => ComputeLeaf(a))
                .OrderBy(l => l.ToHex())
                .ToList();

            if (leaves.Count == 0)
                return new byte[32];

            while (leaves.Count > 1)
            {
                var nextLevel = new List<byte[]>();
                for (int i = 0; i < leaves.Count; i += 2)
                {
                    if (i + 1 < leaves.Count)
                    {
                        var combined = CombineHashes(leaves[i], leaves[i + 1]);
                        nextLevel.Add(combined);
                    }
                    else
                    {
                        nextLevel.Add(leaves[i]);
                    }
                }
                leaves = nextLevel;
            }

            return leaves[0];
        }

        public byte[][] ComputeMerkleProof(string address, IEnumerable<string> allAddresses)
        {
            var leaves = allAddresses
                .Select(a => (Address: a, Leaf: ComputeLeaf(a)))
                .OrderBy(x => x.Leaf.ToHex())
                .ToList();

            var targetLeaf = ComputeLeaf(address);
            var targetIndex = leaves.FindIndex(x => x.Leaf.SequenceEqual(targetLeaf));

            if (targetIndex < 0)
                return Array.Empty<byte[]>();

            var proof = new List<byte[]>();
            var currentLevel = leaves.Select(x => x.Leaf).ToList();

            while (currentLevel.Count > 1)
            {
                var nextLevel = new List<byte[]>();
                for (int i = 0; i < currentLevel.Count; i += 2)
                {
                    if (i + 1 < currentLevel.Count)
                    {
                        if (i == targetIndex || i + 1 == targetIndex)
                        {
                            proof.Add(i == targetIndex ? currentLevel[i + 1] : currentLevel[i]);
                            targetIndex = i / 2;
                        }
                        var combined = CombineHashes(currentLevel[i], currentLevel[i + 1]);
                        nextLevel.Add(combined);
                    }
                    else
                    {
                        if (i == targetIndex)
                        {
                            targetIndex = i / 2;
                        }
                        nextLevel.Add(currentLevel[i]);
                    }
                }
                currentLevel = nextLevel;
            }

            return proof.ToArray();
        }

        public bool VerifyMerkleProof(string address, byte[] root, byte[][] proof)
        {
            if (root == null || root.Length != 32)
                return false;

            if (root.All(b => b == 0))
                return true;

            var computedHash = ComputeLeaf(address);

            foreach (var proofElement in proof)
            {
                computedHash = CombineHashes(computedHash, proofElement);
            }

            return computedHash.SequenceEqual(root);
        }

        public MigrationData PrepareMigrationData(BootstrapPolicyConfig config)
        {
            return new MigrationData
            {
                WritersRoot = ComputeMerkleRoot(config.AllowedWriters),
                AdminsRoot = ComputeMerkleRoot(config.AllowedAdmins),
                WriterProofs = config.AllowedWriters.ToDictionary(
                    w => w,
                    w => ComputeMerkleProof(w, config.AllowedWriters)),
                AdminProofs = config.AllowedAdmins.ToDictionary(
                    a => a,
                    a => ComputeMerkleProof(a, config.AllowedAdmins)),
                MaxCalldataBytes = config.MaxCalldataBytes,
                MaxLogBytes = config.MaxLogBytes,
                BlockGasLimit = config.BlockGasLimit,
                SequencerAddress = config.SequencerAddress ?? ""
            };
        }

        private byte[] ComputeLeaf(string address)
        {
            var addressBytes = address.HexToByteArray();
            if (addressBytes.Length < 20)
            {
                var padded = new byte[20];
                Array.Copy(addressBytes, 0, padded, 20 - addressBytes.Length, addressBytes.Length);
                addressBytes = padded;
            }
            return _keccak.CalculateHash(addressBytes);
        }

        private byte[] CombineHashes(byte[] left, byte[] right)
        {
            var leftHex = left.ToHex();
            var rightHex = right.ToHex();

            byte[] combined;
            if (string.Compare(leftHex, rightHex, StringComparison.OrdinalIgnoreCase) <= 0)
            {
                combined = left.Concat(right).ToArray();
            }
            else
            {
                combined = right.Concat(left).ToArray();
            }

            return _keccak.CalculateHash(combined);
        }
    }

    public class MigrationData
    {
        public byte[] WritersRoot { get; set; } = new byte[32];

        public byte[] AdminsRoot { get; set; } = new byte[32];

        public Dictionary<string, byte[][]> WriterProofs { get; set; } = new Dictionary<string, byte[][]>();

        public Dictionary<string, byte[][]> AdminProofs { get; set; } = new Dictionary<string, byte[][]>();

        public System.Numerics.BigInteger MaxCalldataBytes { get; set; }

        public System.Numerics.BigInteger MaxLogBytes { get; set; }

        public System.Numerics.BigInteger BlockGasLimit { get; set; }

        public string SequencerAddress { get; set; } = "";
    }
}
