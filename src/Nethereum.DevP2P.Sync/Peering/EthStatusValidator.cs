using System;
using Nethereum.EVM.ForkId;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model.P2P;
using Nethereum.Util;

namespace Nethereum.DevP2P.Sync.Peering
{
    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "EthStatusExpectation — what a peer Status must match")]
    public sealed class EthStatusExpectation
    {
        public ulong NetworkId { get; set; }
        public byte[] GenesisHash { get; set; }
        public ulong[] ForkBlocks { get; set; }
        public ulong[] ForkTimestamps { get; set; }
        public ulong HeadBlock { get; set; }
        public ulong HeadTimestamp { get; set; }
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "DecodedEthStatus — a decoded Status and its latest block")]
    public sealed class DecodedEthStatus
    {
        public Eth68StatusMessage Status { get; set; }
        public ulong LatestBlock { get; set; }
    }

    [Nethereum.Documentation.NethereumDocExample(Nethereum.Documentation.DocSection.DevP2P, "devp2p-sync", "EthStatusValidator — one Status rule for every peer")]
    public static class EthStatusValidator
    {
        public static DecodedEthStatus Decode(int ethVersion, byte[] payload)
        {
            if (ethVersion >= 69)
            {
                var s = Eth69StatusMessageEncoder.Decode(payload);
                var rangeViolation = BlockRangeValidator.Violation(s.EarliestBlock, s.LatestBlock, s.LatestBlockHash);
                if (rangeViolation != null)
                    throw new InvalidOperationException($"Peer sent an invalid block range: {rangeViolation}");
                return new DecodedEthStatus
                {
                    LatestBlock = s.LatestBlock,
                    Status = new Eth68StatusMessage
                    {
                        ProtocolVersion = s.ProtocolVersion,
                        NetworkId = s.NetworkId,
                        GenesisHash = s.GenesisHash,
                        ForkHash = s.ForkHash,
                        ForkNext = s.ForkNext,
                        BestHash = s.LatestBlockHash,
                    }
                };
            }
            return new DecodedEthStatus { Status = Eth68StatusMessageEncoder.Decode(payload) };
        }

        public static string Violation(EthStatusExpectation expected, int negotiatedVersion, Eth68StatusMessage remote)
        {
            if (expected != null && remote.NetworkId != expected.NetworkId)
                return $"Peer is on network {remote.NetworkId}, expected {expected.NetworkId}";
            if (remote.ProtocolVersion != negotiatedVersion)
                return $"Peer Status protocol version {remote.ProtocolVersion} != negotiated {negotiatedVersion}";
            if (expected == null) return null;
            if (!ByteUtil.AreEqual(remote.GenesisHash, expected.GenesisHash))
                return $"Peer genesis {remote.GenesisHash.ToHex()} != expected genesis {expected.GenesisHash.ToHex()}";
            return ForkIdViolation(expected, remote);
        }

        private static string ForkIdViolation(EthStatusExpectation expected, Eth68StatusMessage remote)
        {
            var hasSchedule = (expected.ForkBlocks?.Length ?? 0) > 0 || (expected.ForkTimestamps?.Length ?? 0) > 0;
            if (!hasSchedule) return null;

            var result = Eip2124ForkIdCalculator.ValidateForkId(
                remote.ForkHash, remote.ForkNext, expected.GenesisHash,
                expected.ForkBlocks ?? Array.Empty<ulong>(), expected.ForkTimestamps ?? Array.Empty<ulong>(),
                expected.HeadBlock, expected.HeadTimestamp);
            return result == Eip2124ValidationResult.Accepted
                ? null
                : $"Peer forkId 0x{remote.ForkHash:x8}/next={remote.ForkNext} rejected by EIP-2124 validation: {result}";
        }
    }
}
