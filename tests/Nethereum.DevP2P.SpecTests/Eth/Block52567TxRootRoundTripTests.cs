using System.Collections.Generic;
using Nethereum.CoreChain;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Model;
using Xunit;

namespace Nethereum.DevP2P.SpecTests.Eth
{
    /// <summary>
    /// Mainnet block 52567 — the block at which DevP2PBlockSource started reporting
    /// "body mismatch: computed_tx_root != header_tx_root" during from-genesis sync.
    ///
    /// The header commits to:
    ///   transactionsRoot = 0x2c1061e0ab22fc2610545f7ba3dee74ddc46d3524be500a127b6f5a724c207de
    ///
    /// The block contains 3 legacy (type 0x00) transactions, captured byte-for-byte
    /// from Erigon's eth_getRawTransactionByHash. If our decode→re-encode round-trip
    /// in <see cref="RlpBlockEncodingProvider"/> is byte-perfect, the computed root
    /// from re-encoded bodies will match the canonical root and the live-sync warning
    /// at block 52567 is a peer-quality issue (peer sent wrong bodies); if not, our
    /// transaction encoder loses fidelity for some legacy-tx shape and DevP2PBlockSource
    /// will *always* mismatch on it regardless of peer.
    /// </summary>
    public class Block52567TxRootRoundTripTests
    {
        // Captured 2026-06-19 via:
        //   curl -s -X POST <erigon>:8545 -d '{"method":"eth_getRawTransactionByHash","params":["0x..."]}'
        // for each tx hash in eth_getBlockByNumber(0xCD57).
        private static readonly string[] RawSignedTxHex = new[]
        {
            "0xf86d0285746a528800825b04943234d29e2390e0ac637b3ad0aca3df88ac82dc5d8904328e9ec21e7f0000801ca0cb5e92c091d2ac41ca12f5c15b79f1d949278be3ecf0af9285c1b47abf1ca104a02e4d0e6c68bf54c14a576783e85292018504b64ccd2847f1bdff825619825658",
            "0xf86f8202dc850e7d27df178307a12094baa54d6e90c3f4d7ebec11bd180134c7ed8ebb5288016345785d8a0000801ca0cef76d650772fc717ce5a489f10c57552a41f098f96f4b2dcdffca617ac44920a07edb3d3ff57410034e255783a91d0d3c004d0de8d488b2ab70d13cb25ffd3b63",
            "0xf86f8202dd850e7d27df178307a12094baa54d6e90c3f4d7ebec11bd180134c7ed8ebb5288016345785d8a0000801ba0d3ab6bc74b02d93fc3de9692021f95d65b1509f5f657b7319c80a27ea1d75f90a06ffcf18f5d62ac48988f2432ab4b0d5716ecd6290fad6edc680cd4a9add7c4b0"
        };

        private const string ExpectedTxRootHex = "0x2c1061e0ab22fc2610545f7ba3dee74ddc46d3524be500a127b6f5a724c207de";

        [Fact]
        public void RawRlps_ComputeTxRoot_MatchesCanonicalHeader()
        {
            var raw = new List<byte[]>();
            foreach (var h in RawSignedTxHex)
                raw.Add(h.HexToByteArray());

            var calc = new RootCalculator();
            var root = calc.CalculateTransactionsRoot(raw);

            Assert.Equal(ExpectedTxRootHex.RemoveHexPrefix(), root.ToHex());
        }

        [Fact]
        public void DecodeReencode_EachTransaction_IsByteIdentical()
        {
            var provider = RlpBlockEncodingProvider.Instance;
            for (int i = 0; i < RawSignedTxHex.Length; i++)
            {
                var raw = RawSignedTxHex[i].HexToByteArray();
                var decoded = provider.DecodeTransaction(raw);
                var reencoded = provider.EncodeTransaction(decoded);

                Assert.True(
                    System.Linq.Enumerable.SequenceEqual(raw, reencoded),
                    $"tx[{i}]: re-encode differs from raw\n  raw=0x{raw.ToHex()}\n  enc=0x{reencoded.ToHex()}");
            }
        }

        [Fact]
        public void ReencodedRlps_ComputeTxRoot_MatchesCanonicalHeader()
        {
            var provider = RlpBlockEncodingProvider.Instance;
            var reencoded = new List<byte[]>();
            foreach (var h in RawSignedTxHex)
            {
                var raw = h.HexToByteArray();
                var decoded = provider.DecodeTransaction(raw);
                reencoded.Add(provider.EncodeTransaction(decoded));
            }

            var calc = new RootCalculator();
            var root = calc.CalculateTransactionsRoot(reencoded);

            Assert.Equal(ExpectedTxRootHex.RemoveHexPrefix(), root.ToHex());
        }
    }
}
