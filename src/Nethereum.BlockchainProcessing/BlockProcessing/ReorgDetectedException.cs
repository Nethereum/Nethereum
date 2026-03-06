using System;
using System.Numerics;

namespace Nethereum.BlockchainProcessing.BlockProcessing
{
    public class ReorgDetectedException : Exception
    {
        public BigInteger RewindToBlockNumber { get; }
        public BigInteger LastCanonicalBlockNumber { get; }
        public string LastCanonicalBlockHash { get; }
        public BigInteger CurrentBlockNumber { get; }
        public string CurrentBlockHash { get; }
        public string CurrentParentHash { get; }

        public ReorgDetectedException(
            BigInteger rewindToBlockNumber,
            BigInteger lastCanonicalBlockNumber,
            string lastCanonicalBlockHash,
            BigInteger currentBlockNumber,
            string currentBlockHash,
            string currentParentHash)
            : base(
                $"Reorg detected. Last canonical block #{lastCanonicalBlockNumber} ({lastCanonicalBlockHash}), " +
                $"current block #{currentBlockNumber} parent {currentParentHash}. " +
                $"Rewind to #{rewindToBlockNumber}.")
        {
            RewindToBlockNumber = rewindToBlockNumber;
            LastCanonicalBlockNumber = lastCanonicalBlockNumber;
            LastCanonicalBlockHash = lastCanonicalBlockHash;
            CurrentBlockNumber = currentBlockNumber;
            CurrentBlockHash = currentBlockHash;
            CurrentParentHash = currentParentHash;
        }
    }
}
