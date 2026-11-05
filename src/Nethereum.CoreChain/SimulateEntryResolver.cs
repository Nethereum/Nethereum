using System.Linq;
using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nethereum.CoreChain.Composition;
using Nethereum.CoreChain.Forks;
using Nethereum.CoreChain.Prefetch;
using Nethereum.CoreChain.State;
using Nethereum.CoreChain.Storage;
using Nethereum.EVM;
using Nethereum.EVM.BlockchainState;
using Nethereum.EVM.Execution;
using Nethereum.EVM.Witness;
using Nethereum.Model;
using Nethereum.Signer;
using Nethereum.Util;
using Nethereum.Hex.HexConvertors.Extensions;
using Nethereum.Hex.HexTypes;
using Nethereum.Merkle.Patricia;
using Nethereum.Merkle.Patricia.Storage;

namespace Nethereum.CoreChain
{
    internal static class SimulateEntryResolver
    {
        internal static async Task<EvmUInt256> ResolveSimulateRunningNonceAsync(IStateStore stateForBlock, TxEntry entry)
        {
            if (entry.SimulateCall == null || entry.SimulateCall.Nonce != null)
                return EvmUInt256.Zero;

            var account = await stateForBlock.GetAccountAsync(entry.CachedSender ?? AddressUtil.ZERO_ADDRESS).ConfigureAwait(false);
            return account?.Nonce ?? EvmUInt256.Zero;
        }

        internal static TxEntry ResolveSimulateEntry(
            TxEntry entry, BigInteger rpcGasCap, long blockGasLimit, long blockGasUsedSoFar,
            BigInteger globalGasUsedBefore, BigInteger chainId, EvmUInt256 runningNonce)
        {
            if (entry.SimulateCall == null)
                return entry;

            var needsNonce = entry.SimulateCall.Nonce == null;
            var blockRemaining = blockGasLimit - blockGasUsedSoFar;
            var requestedGas = entry.SimulateCall.Gas != null ? entry.SimulateCall.Gas.Value : (BigInteger)blockRemaining;
            var budgetRemaining = rpcGasCap > 0
                ? rpcGasCap - (globalGasUsedBefore + blockGasUsedSoFar)
                : requestedGas;
            var gas = BigInteger.Max(0, BigInteger.Min(requestedGas, budgetRemaining));

            if (!needsNonce && entry.SimulateCall.Gas != null && gas == requestedGas)
                return entry;

            var nonce = entry.SimulateCall.Nonce ?? new HexBigInteger((BigInteger)runningNonce);

            var resolvedCall = new Nethereum.RPC.Eth.DTOs.TransactionInput
            {
                From = entry.SimulateCall.From,
                To = entry.SimulateCall.To,
                Data = entry.SimulateCall.Data,
                Value = entry.SimulateCall.Value,
                Gas = new HexBigInteger(gas),
                GasPrice = entry.SimulateCall.GasPrice,
                MaxFeePerGas = entry.SimulateCall.MaxFeePerGas,
                MaxPriorityFeePerGas = entry.SimulateCall.MaxPriorityFeePerGas,
                MaxFeePerBlobGas = entry.SimulateCall.MaxFeePerBlobGas,
                BlobVersionedHashes = entry.SimulateCall.BlobVersionedHashes,
                Nonce = nonce
            };

            var tx = BuildSyntheticSimulateTransaction(resolvedCall, chainId);
            return new TxEntry(tx, entry.CachedSender, resolvedCall);
        }

        internal static ISignedTransaction BuildSyntheticSimulateTransaction(Nethereum.RPC.Eth.DTOs.TransactionInput call, BigInteger chainId)
        {
            if (call.BlobVersionedHashes != null && call.BlobVersionedHashes.Count > 0)
            {
                return new Transaction4844(
                    chainId: (EvmUInt256)chainId,
                    nonce: call.Nonce != null ? (EvmUInt256)call.Nonce.Value : EvmUInt256.Zero,
                    maxPriorityFeePerGas: call.MaxPriorityFeePerGas != null ? (EvmUInt256)call.MaxPriorityFeePerGas.Value : EvmUInt256.Zero,
                    maxFeePerGas: call.MaxFeePerGas != null ? (EvmUInt256)call.MaxFeePerGas.Value : EvmUInt256.Zero,
                    gasLimit: call.Gas != null ? (EvmUInt256)call.Gas.Value : EvmUInt256.Zero,
                    receiverAddress: call.To ?? string.Empty,
                    amount: call.Value != null ? (EvmUInt256)call.Value.Value : EvmUInt256.Zero,
                    data: call.Data ?? "0x",
                    accessList: null,
                    maxFeePerBlobGas: call.MaxFeePerBlobGas != null ? (EvmUInt256)call.MaxFeePerBlobGas.Value : EvmUInt256.Zero,
                    blobVersionedHashes: call.BlobVersionedHashes.Select(h => h.HexToByteArray()).ToList());
            }

            return new Transaction1559(
                chainId: (EvmUInt256)chainId,
                nonce: call.Nonce != null ? (EvmUInt256)call.Nonce.Value : EvmUInt256.Zero,
                maxPriorityFeePerGas: call.MaxPriorityFeePerGas != null ? (EvmUInt256)call.MaxPriorityFeePerGas.Value : EvmUInt256.Zero,
                maxFeePerGas: call.MaxFeePerGas != null ? (EvmUInt256)call.MaxFeePerGas.Value : EvmUInt256.Zero,
                gasLimit: call.Gas != null ? (EvmUInt256)call.Gas.Value : EvmUInt256.Zero,
                receiverAddress: call.To ?? string.Empty,
                amount: call.Value != null ? (EvmUInt256)call.Value.Value : EvmUInt256.Zero,
                data: call.Data ?? "0x",
                accessList: null);
        }
    }
}
