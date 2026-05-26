using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;

namespace Nethereum.AppChain.Anchoring
{
    public class MockAnchorService : IAnchorService
    {
        private readonly ConcurrentDictionary<BigInteger, AnchorInfo> _anchors = new();

        public IReadOnlyDictionary<BigInteger, AnchorInfo> Anchors => _anchors;
        public byte[]? LastBlockHash { get; private set; }
        public AnchorSubmissionPayload? LastSubmission { get; private set; }

        public Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber, byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot)
        {
            return AnchorBlockAsync(blockNumber, stateRoot, transactionsRoot, receiptsRoot, null, null);
        }

        public Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber, byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot, byte[] extraData)
        {
            return AnchorBlockAsync(blockNumber, stateRoot, transactionsRoot, receiptsRoot, null,
                extraData != null ? new AnchorSubmissionPayload { ProofBytes = extraData } : null);
        }

        public Task<AnchorInfo> AnchorBlockAsync(
            BigInteger blockNumber, byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot,
            byte[] blockHash, AnchorSubmissionPayload? submission)
        {
            LastBlockHash = blockHash;
            LastSubmission = submission;
            var txHash = new byte[32];
            BitConverter.GetBytes((long)blockNumber).CopyTo(txHash, 0);

            var info = new AnchorInfo
            {
                BlockNumber = blockNumber,
                StateRoot = stateRoot,
                TransactionsRoot = transactionsRoot,
                ReceiptsRoot = receiptsRoot,
                ExtraData = submission?.ProofBytes,
                Timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds(),
                AnchorTxHash = txHash,
                AnchorBlockNumber = blockNumber,
                Status = AnchorStatus.Confirmed
            };

            _anchors[blockNumber] = info;
            return Task.FromResult(info);
        }

        public Task<AnchorInfo?> GetAnchorAsync(BigInteger blockNumber)
        {
            _anchors.TryGetValue(blockNumber, out var info);
            return Task.FromResult(info);
        }

        public Task<BigInteger> GetLatestAnchoredBlockAsync()
        {
            BigInteger latest = 0;
            foreach (var k in _anchors.Keys)
                if (k > latest) latest = k;
            return Task.FromResult(latest);
        }

        public Task<bool> VerifyAnchorAsync(
            BigInteger blockNumber, byte[] stateRoot, byte[] transactionsRoot, byte[] receiptsRoot)
        {
            if (_anchors.TryGetValue(blockNumber, out var info))
            {
                return Task.FromResult(
                    Nethereum.Util.ByteUtil.AreEqual(info.StateRoot, stateRoot) &&
                    Nethereum.Util.ByteUtil.AreEqual(info.TransactionsRoot, transactionsRoot) &&
                    Nethereum.Util.ByteUtil.AreEqual(info.ReceiptsRoot, receiptsRoot));
            }
            return Task.FromResult(false);
        }
    }
}
