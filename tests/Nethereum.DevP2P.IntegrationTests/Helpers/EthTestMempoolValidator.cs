using System;
using System.Collections.Concurrent;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    /// <summary>
    /// Validates incoming transactions for the eth-test conformance harness — the
    /// minimum set of mempool checks that go-ethereum's <c>TestInvalidTxs</c>
    /// sub-test exercises (nonce, intrinsic gas, block gas limit, balance) plus
    /// a future-nonce window so a peer can't flood us with far-out reservations.
    /// <para>
    /// Reads sender state from the head <see cref="IStateStore"/> built by the
    /// historical-state replay; tracks per-sender pending nonces so a follow-up
    /// tx at the same nonce is rejected as a collision.
    /// </para>
    /// </summary>
    public class EthTestMempoolValidator
    {
        /// <summary>
        /// Maximum gap between a sender's current chain nonce and an accepted future tx.
        /// 16 mirrors go-ethereum's <c>core/txpool/legacypool.MaxFutureBlocks</c> tolerance
        /// — large enough for normal nonce streams, small enough to bound memory.
        /// </summary>
        public const int FutureNonceWindow = 16;

        private readonly IStateStore _headState;
        private readonly EvmUInt256 _blockGasLimit;
        private readonly IntrinsicGasRules _intrinsicGas;
        private readonly ConcurrentDictionary<string, EvmUInt256> _pendingNonces = new();

        public EthTestMempoolValidator(IStateStore headState, ulong blockGasLimit, IntrinsicGasRules intrinsicGas = null)
        {
            _headState = headState ?? throw new ArgumentNullException(nameof(headState));
            _blockGasLimit = new EvmUInt256(blockGasLimit);
            // Testdata chain reaches Cancun; intrinsic gas rules at head are EIP-2028 + EIP-2930 + EIP-3860 + EIP-4844.
            _intrinsicGas = intrinsicGas ?? IntrinsicGasRuleSets.Cancun;
        }

        /// <summary>
        /// Returns true if <paramref name="tx"/> would be admitted to a mempool —
        /// signature recovers, nonce is in window, gas covers intrinsic and fits
        /// the block, and the sender's balance covers <c>value + gas × maxFeePerGas</c>.
        /// </summary>
        public bool IsValid(ISignedTransaction tx, string preRecoveredSender = null)
        {
            try
            {
                var sender = preRecoveredSender ?? tx.GetSenderAddress();
                if (string.IsNullOrEmpty(sender)) return false;
                var senderKey = sender.ToLowerInvariant();

                var account = _headState.GetAccountAsync(senderKey).GetAwaiter().GetResult();
                var senderNonce = account?.Nonce ?? EvmUInt256.Zero;
                var senderBalance = account?.Balance ?? EvmUInt256.Zero;

                var txNonce = tx.GetNonce();
                var txGasLimit = tx.GetGasLimit();
                var txValue = tx.GetValue();
                var txMaxFeePerGas = tx.GetMaxFeePerGas();
                var txData = tx.GetData() ?? Array.Empty<byte>();

                // Mempool nonce rule: next acceptable nonce is (highest-pending + 1) if we already
                // have a pending tx from this sender, else the sender's on-chain nonce. Rejects
                // both low-nonce replay and same-nonce collisions (geth's TestInvalidTxs uses both).
                var nextExpected = _pendingNonces.TryGetValue(senderKey, out var pending)
                    ? pending + EvmUInt256.One
                    : senderNonce;
                if (txNonce < nextExpected) return false;
                if (txNonce > nextExpected + new EvmUInt256((ulong)FutureNonceWindow)) return false;

                if (txGasLimit > _blockGasLimit) return false;

                var intrinsic = _intrinsicGas.CalculateIntrinsicGas(txData, tx.IsContractCreation(), accessList: null);
                if (txGasLimit < new EvmUInt256((ulong)intrinsic)) return false;

                var cost = txValue + txGasLimit * txMaxFeePerGas;
                if (senderBalance < cost) return false;

                _pendingNonces.AddOrUpdate(senderKey, txNonce, (_, v) => txNonce > v ? txNonce : v);
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
