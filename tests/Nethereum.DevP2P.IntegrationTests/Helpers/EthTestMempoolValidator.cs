using System;
using System.Collections.Concurrent;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM.Gas;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;

namespace Nethereum.DevP2P.IntegrationTests.Helpers
{
    public class EthTestMempoolValidator
    {
        public const int FutureNonceWindow = 16;

        private readonly IStateStore _headState;
        private readonly EvmUInt256 _blockGasLimit;
        private readonly IntrinsicGasRules _intrinsicGas;
        private readonly ConcurrentDictionary<string, EvmUInt256> _pendingNonces = new();

        public EthTestMempoolValidator(IStateStore headState, ulong blockGasLimit, IntrinsicGasRules intrinsicGas = null)
        {
            _headState = headState ?? throw new ArgumentNullException(nameof(headState));
            _blockGasLimit = new EvmUInt256(blockGasLimit);
            _intrinsicGas = intrinsicGas ?? IntrinsicGasRuleSets.Cancun;
        }

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

                var nextExpected = _pendingNonces.TryGetValue(senderKey, out var pending)
                    ? pending + EvmUInt256.One
                    : senderNonce;
                if (txNonce < nextExpected) return false;
                if (txNonce > nextExpected + new EvmUInt256((ulong)FutureNonceWindow)) return false;

                if (txGasLimit > _blockGasLimit) return false;

                var isContractCreation = tx.IsContractCreation();
                var isSelfTransfer = !isContractCreation && sender.IsTheSameAddress(tx.GetReceiverAddress());
                var hasValue = !txValue.IsZero;

                var intrinsic = _intrinsicGas.CalculateIntrinsicGas(txData, isContractCreation, accessList: null, isSelfTransfer, hasValue);
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
