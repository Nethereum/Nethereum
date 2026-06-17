using System;
using System.IO;
using Nethereum.Model;
using Nethereum.RLP;

namespace Nethereum.DevP2P.IntegrationTests
{
    /// <summary>
    /// Test-only reader for go-ethereum's `cmd/devp2p/internal/ethtest/testdata/chain.rlp`
    /// fixture. The chain.rlp format is a Geth/Hive tooling convention: a
    /// stream of independently RLP-encoded blocks `[header, txs, uncles, withdrawals?]`
    /// concatenated together. Used solely to extract genesis hash for the
    /// snap-test conformance harness — block 1's parentHash IS the genesis hash.
    /// </summary>
    public static class GethChainRlpFixtureReader
    {
        /// <summary>
        /// Reads the very first block from chain.rlp and returns its parentHash
        /// — which by definition is the genesis block's hash.
        /// </summary>
        public static byte[] ReadGenesisHash(string chainRlpPath)
        {
            var fileBytes = File.ReadAllBytes(chainRlpPath);

            // RLP.DecodeFirstElement requires a startPos; the file begins with
            // a top-level RLP list (the first block). Decode it and the result
            // is the [header, txs, uncles, withdrawals?] collection.
            var firstBlock = (RLPCollection)RLP.RLP.DecodeFirstElement(fileBytes, 0);
            var firstHeader = (RLPCollection)firstBlock[0];

            // Per BlockHeaderEncoder.Decode field order, element 0 is parentHash.
            return firstHeader[0].RLPData
                ?? throw new InvalidOperationException("chain.rlp first block header missing parentHash field");
        }
    }
}
